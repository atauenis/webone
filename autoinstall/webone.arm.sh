#!/usr/bin/env bash
clear
echo -e "\n\033[32m               ______"
echo -e "___      _________  /___________________ "
echo -e "__ | /| / /  _ \\_  __ \\  __ \\_  __ \\  _ \\"
echo -e "__ |/ |/ //  __/  /_/ / /_/ /  / / /  __/"
echo -e "____/|__/ \\___//_.___/\\____//_/ /_/\\___/ "
echo -e "----------------------------------------"
echo -e "                    by Alexander Tauenis"
echo -e "\n\033[0m\n"
RD="$(cd $(dirname "$0") && pwd)"
SERVICE="
[Unit]
Description=WebOne HTTP(S) Proxy Server
Documentation=https://github.com/atauenis/webone/wiki/
Requires=network-online.target
After=network-online.target

[Service]
Type=simple
DynamicUser=yes
Environment="HOME=/tmp/"
ExecStart=/usr/local/bin/webone --daemon -cfg "/etc/webone/webone.conf"
ReadWriteDirectories=-/var/log/
TimeoutStopSec=10
Restart=on-failure
RestartSec=5
StartLimitInterval=5s
StartLimitBurst=3

[Install]
WantedBy=default.target
"
echo -e "\033[1;35m- PREPARE\033[0m\n"
if [ ! -d "$RD/webone" ]; then
    git clone -b master --depth=1 --single-branch https://github.com/atauenis/webone.git && cd webone
    git config pull.rebase false
else 
    cd "$RD/webone" && git pull
fi 
cd $RD
echo -e "\n\033[1;35m- INSTALLATION\033[0m\n"
systemctl stop webone > /dev/null 2>&1
sleep 2
if [ -d "/usr/local/webone" ]; then
    echo -e "- entering /usr/local/webone"
    cd /usr/local/webone
    rm -fR ./*  > /dev/null 2>&1
else
    mkdir -p /usr/local/webone
    chmod 0744 /usr/local/webone
fi
if [ -d "/etc/webone" ]; then
    echo -e "- entering /etc/webone"
    cd /etc/webone
    rm ./webone.conf.old > /dev/null 2>&1
    mv ./webone.conf ./webone.conf.old
else
    mkdir /etc/webone
    chmod -r 755 /etc/webone
fi
if [ ! -f "/var/log/webone.log" ]; then
    touch /var/log/webone.log
    chmod 666 /var/log/webone.log
fi
echo -e "- entering $RD/webone"
cd $RD/webone
rm -rf ./webone-build > /dev/null 2>&1
# trying to install dotnet
if [ "$(which dotnet)" == "" ]; then
    wget https://dot.net/v1/dotnet-install.sh 
    chmod +x ./dotnet-install.sh
    ./dotnet-install.sh -c 7.0
    rm dotnet-install.sh
    ln -s /root/.dotnet/dotnet  /usr/local/bin
fi
echo -e "\n-- Build\n"
dotnet build ./WebOne.csproj -r linux-arm64
echo -e "\n-- Make\n"
dotnet publish ./WebOne.csproj -c Release -r linux-arm64 --self-contained -o ./webone-build
echo -e "\n-- Install\n"
cd ./webone-build
cp webone.conf /etc/webone
cp codepage.conf /etc/webone
# fixing: unable to create CA cert
chmod 755 /etc/webone
if [ ! -f "/etc/logrotate.d/webone" ]; then
    mv webone.logrotate /etc/logrotate.d/webone
else
    rm webone.logrotate > /dev/null 2>&1
fi
# creating service
if [ ! -f "/etc/systemd/system/webone.service" ]; then
    echo $SERVICE > /etc/systemd/system/webone.service
else 
    rm webone.service
fi
# cleanup
rm README.md CONTRIBUTING.md  > /dev/null 2>&1
# moving program
cp -pr ./* /usr/local/webone/
# linking executables
if [ ! -f "/usr/local/bin/webone" ]; then
    ln -s /usr/local/webone/webone /usr/local/bin/webone
fi
# grant access
chmod a+x /usr/local/webone /usr/local/webone/webone
echo -e "- initializing webone service"
systemctl daemon-reload
systemctl enable webone
systemctl start webone
sleep 2
systemctl status webone
echo -e "\n-- Cleanup\n"
cd ..
dotnet clean WebOne.csproj
rm -r ./webone-build
echo -e "\n\033[1;35m- ALL DONE\033[0m\n"
exit 0