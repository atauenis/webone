# How to help the project?

You can help WebOne project by finding and reporting bugs, by tweaking configuration files and traffic edit sets, or by writing program's code. Please follow this guide when making your contributions.

## Do you found a bug?
The bug tracker is located at [GitHub Issues](https://github.com/atauenis/webone/issues). It is a great place to report what don't work as expected.

Currently all Issues are marking as Closed after they gets fixed in source code. So if you find a bug in latest stable release ![](https://img.shields.io/github/v/tag/atauenis/webone?include_prereleases&label=WebOne), check if it is already known in [all bugs](https://github.com/atauenis/webone/issues?q=is%3Aissue+label%3Abug+sort%3Aupdated-desc) list. When source code in `dev` collects a large amount of fixed bugs, it moves to `master` and becomes a next stable release of WebOne.

## Did you write a patch that fixes a bug?
It is great! Make a Pull Request with the patch, and it will be accepted after review.

## Did you made a traffic edit set which fixes work of a web site over WebOne?
Thanks. It's a time to start a topic in [Website patches](https://github.com/atauenis/webone/discussions/categories/website-patches). And anyone will be able to copy the edit set self, and surf that site via old browser. Probably also it will be accepted into main WebOne code base and the edit set will be included in some of next version.

## Do you have ideas about new features?
[GitHub Discussions](https://github.com/atauenis/webone/discussions) is a great place for talks about need features. Also there is a semi-official list of features, planned for nearest releases: [Project roadmap](https://github.com/atauenis/webone/wiki/Project-roadmap).

## Do you want to implement new features?
Great. The main project developer have small amount of time to work in Visual Studio / .NET SDK and write good code, so such help would be very good for the project. Similar to bug fixes, Forks & Pull Requests on Git are the way to work with contributions.

## Why C# is chosen for WebOne?
Microsoft .NET is a cross-platform framework, which primary programming language is C#. It is easy for rapid development, have ready libraries for many tasks (HTTP, HTTPS, stdin/stdout, [DEB/RPM](https://github.com/qmfrederik/dotnet-packaging/), etc), is open source, works on many platforms and systems. And the main project developer knows this language enough well to write such server. :)

## C# language and .NET SDK version note
The current version of WebOne is built using latest LTS version of .NET SDK (and runs via corresponding .NET Runtime environment). But as long at it is possible the main developer wants to keep ability to build WebOne for older systems like Windows 7 (32-bit), Raspbian 10 (ARMv6), macOS 10.14 (Intel). They supports .NET 6 or .NET 8 maximum. So please test the code on both .NET LTS (dotnet-sdk-10.0, as of this article write time) and .NET 6.0 @ win7/8/10.

## Language for speak
The public talk language is English. Although @atauenis is also speaking Russian, please have respect for the all community. Any questions in Russian can be asked via bulltin boards where it is allowed (or private messages).

## Remind please, where is main code repository?
The project currently hosts on GitHub ecosystem: 
* https://github.com/atauenis/webone.git
* https://github.com/atauenis/webone/archive/refs/heads/master.zip ([Archived](https://web.archive.org/web/*/https://github.com/atauenis/webone/archive/refs/heads/master.zip))
* https://github.com/atauenis/webone/wiki - Wiki documentation ([Git URL](https://github.com/atauenis/webone.wiki.git))