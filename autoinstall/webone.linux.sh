#!/usr/bin/env bash
clear
echo -e "\n\033[32m               ______"
echo -e "___      _________  /___________________ "
echo -e "__ | /| / /  _ \\_  __ \\  __ \\_  __ \\  _ \\"
echo -e "__ |/ |/ //  __/  /_/ / /_/ /  / / /  __/"
echo -e "____/|__/ \\___//_.___/\\____//_/ /_/\\___/ "
echo -e "----------------------------------------"
echo -e "                    by Alexander Tauenis"
echo -e "\033[0m"
RD="$(cd $(dirname "$0") && pwd)"
ARGS=$@
RUNTM="linux-x64"
MOD=0
for n in $ARGS; do
    if [ "$n" = "-i" ]; then
        MOD=1
    elif [ "$n" = "-u" ]; then
        MOD=2
    elif [ "$n" = "-arm" ]; then
        RUNTM="linux-arm"
    elif [ "$n" = "-arm64" ]; then
        RUNTM="linux-arm64"
    elif [ "$n" = "-musl" ]; then
        RUNTM="linux-musl-x64"
    elif [ "$n" = "-musl-arm" ]; then
        RUNTM="linux-musl-arm64"
    elif [ "$n" = "-android" ]; then
        RUNTM="linux-bionic-arm64"
    fi
done
## installing by default
if [ $MOD -eq 1 ]; then
    # check if the necessary utilities are installed and accessible
    if ! command -v git &> /dev/null; then
        echo -e "\033[1;31m(!) please, install git\033[0m\n"
        exit 0
    fi
    # getting things ready
    echo -e "\033[1;35m- PREPARE\033[0m\n"
    if [ ! -d "$RD/webone" ]; then
        git config --global http.version HTTP/1.1
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
        if [ -f "./webone.conf" ]; then
            mv ./webone.conf ./webone.conf.old
        fi
    else
        mkdir /etc/webone
        chmod -R 755 /etc/webone
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
        # optout on telemetry
        set DOTNET_CLI_TELEMETRY_OPTOUT=1
    fi
    echo -e "\n-- Build\n"
    dotnet build ./WebOne.csproj -r $RUNTM
    echo -e "\n-- Make\n"
    dotnet publish ./WebOne.csproj -c Release -r $RUNTM --self-contained -o ./webone-build
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
        echo -e -n  "[Unit]\n"\
                    "Description=WebOne HTTP(S) Proxy Server\n"\
                    "Documentation=https://github.com/atauenis/webone/wiki/\n"\
                    "Requires=network-online.target\n"\
                    "After=network-online.target\n\n"\
                    "[Service]\n"\
                    "Type=simple\n"\
                    "DynamicUser=yes\n"\
                    "Environment=\"HOME=/tmp/\"\n"\
                    "ExecStart=/usr/local/bin/webone --daemon -cfg \"/etc/webone/webone.conf\"\n"\
                    "ReadWriteDirectories=-/var/log/\n"\
                    "TimeoutStopSec=10\n"\
                    "Restart=on-failure\n"\
                    "RestartSec=5\n"\
                    "StartLimitInterval=5s\n"\
                    "StartLimitBurst=3\n\n"\
                    "[Install]\n"\
                    "WantedBy=default.target\n" > /etc/systemd/system/webone.service
    else
        rm webone.service > /dev/null 2>&1
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
    rm -r ./webone-build > /dev/null 2>&1
elif [ $MOD -eq 2 ]; then                   # MOD -ne 1
    ## uninstall and cleanup
    echo -e "\n\033[1;35m- UNINSTALL\033[0m\n"
    systemctl stop webone
    systemctl disable webone
    systemctl daemon-reload
    rm -f /etc/systemd/system/webone.service > /dev/null 2>&1
    rm -fr /usr/local/webone > /dev/null 2>&1
    rm -fr /etc/webone > /dev/null 2>&1
else                                        # MOD
    echo -e "\033[34mUsage:\n"
    echo    "    [-i]        - install as a service"
    echo -e "    [-u]        - completely uninstall webone, including configuration\n"
    echo    "  Default building mode is for linux-x64 platforms which are most desktop distributions"
    echo -e "  like CentOS, Debian, Fedora, Ubuntu, and derivatives.\n"
    echo    "    [-arm]      - Linux distributions running on Arm like Raspbian on Raspberry Pi Model 2+"
    echo    "    [-arm64]    - Linux distributions running on 64-bit Arm like Ubuntu Server"
    echo    "                  64-bit on Raspberry Pi Model 3+"
    echo    "    [-musl]     - Lightweight distributions using musl like Alpine Linux"
    echo    "    [-musl-arm] - Used to build Docker images for 64-bit Arm v8 and minimalistic base images"
    echo -e "    [-android]  - Distributions using Android's bionic libc, for example, Termux\n\033[0m"
    exit 0
fi                                          # MOD
echo -e "\n\033[1;35m- ALL DONE\033[0m\n"
exit 0