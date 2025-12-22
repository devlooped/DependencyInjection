# Changelog

## [v2.3.1](https://github.com/devlooped/DependencyInjection/tree/v2.3.1) (2025-12-22)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v2.3.0...v2.3.1)

:sparkles: Implemented enhancements:

- Add ServiceAttribute\<TService\> to allow specifying a service type [\#281](https://github.com/devlooped/DependencyInjection/issues/281)
- Provide a codefix for legacy service usage and add tests [\#303](https://github.com/devlooped/DependencyInjection/pull/303) (@kzu)

:twisted_rightwards_arrows: Merged:

- Fix dependency on package version for version poke [\#305](https://github.com/devlooped/DependencyInjection/pull/305) (@kzu)

## [v2.3.0](https://github.com/devlooped/DependencyInjection/tree/v2.3.0) (2025-12-17)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v2.2.2...v2.3.0)

:sparkles: Implemented enhancements:

- Repurpose older \[Service\<T\>\] to allow specifying a service type [\#296](https://github.com/devlooped/DependencyInjection/pull/296) (@kzu)

## [v2.2.2](https://github.com/devlooped/DependencyInjection/tree/v2.2.2) (2025-12-17)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v2.2.1...v2.2.2)

:bug: Fixed bugs:

- Avoid all service-related codegen when AddServicesExtension=false [\#295](https://github.com/devlooped/DependencyInjection/pull/295) (@kzu)

## [v2.2.1](https://github.com/devlooped/DependencyInjection/tree/v2.2.1) (2025-10-29)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v2.2.0...v2.2.1)

:twisted_rightwards_arrows: Merged:

- Minor tweaks to readme and legacy dependency [\#277](https://github.com/devlooped/DependencyInjection/pull/277) (@kzu)

## [v2.2.0](https://github.com/devlooped/DependencyInjection/tree/v2.2.0) (2025-10-25)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v2.1.0...v2.2.0)

:sparkles: Implemented enhancements:

- Drop SponsorLink, adopt OSMF [\#258](https://github.com/devlooped/DependencyInjection/pull/258) (@kzu)

:hammer: Other:

- 💟 Adopting SponsorLink in v2.1 [\#126](https://github.com/devlooped/DependencyInjection/issues/126)

:twisted_rightwards_arrows: Merged:

- Update obsolete package to latest released version [\#163](https://github.com/devlooped/DependencyInjection/pull/163) (@kzu)

## [v2.1.0](https://github.com/devlooped/DependencyInjection/tree/v2.1.0) (2025-05-11)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v2.1.0-rc.5...v2.1.0)

## [v2.1.0-rc.5](https://github.com/devlooped/DependencyInjection/tree/v2.1.0-rc.5) (2025-04-24)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v2.1.0-rc.4...v2.1.0-rc.5)

:sparkles: Implemented enhancements:

- Don't generate any of the registrations during design-time [\#155](https://github.com/devlooped/DependencyInjection/pull/155) (@kzu)

:twisted_rightwards_arrows: Merged:

- Don't strong-name anything [\#149](https://github.com/devlooped/DependencyInjection/pull/149) (@kzu)

## [v2.1.0-rc.4](https://github.com/devlooped/DependencyInjection/tree/v2.1.0-rc.4) (2025-04-23)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v2.1.0-rc.3...v2.1.0-rc.4)

:bug: Fixed bugs:

- Don't emit registrations with generics that don't satisfy constraints [\#148](https://github.com/devlooped/DependencyInjection/pull/148) (@kzu)
- Fix path of compile items within package [\#147](https://github.com/devlooped/DependencyInjection/pull/147) (@kzu)
- Add extra check for non-null ReducedFrom [\#146](https://github.com/devlooped/DependencyInjection/pull/146) (@kzu)
- Make sure we don't attempt to analyze methods without a symbol [\#145](https://github.com/devlooped/DependencyInjection/pull/145) (@kzu)

:hammer: Other:

- Add support to register internal classes [\#107](https://github.com/devlooped/DependencyInjection/issues/107)

## [v2.1.0-rc.3](https://github.com/devlooped/DependencyInjection/tree/v2.1.0-rc.3) (2024-12-06)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v2.1.0-rc.2...v2.1.0-rc.3)

:sparkles: Implemented enhancements:

- Don't report inconsitencies if lifetimes match [\#135](https://github.com/devlooped/DependencyInjection/pull/135) (@kzu)

## [v2.1.0-rc.2](https://github.com/devlooped/DependencyInjection/tree/v2.1.0-rc.2) (2024-12-06)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v2.1.0-rc.1...v2.1.0-rc.2)

## [v2.1.0-rc.1](https://github.com/devlooped/DependencyInjection/tree/v2.1.0-rc.1) (2024-12-06)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v2.1.0-rc...v2.1.0-rc.1)

:sparkles: Implemented enhancements:

- Report inconsistent lifetime registrations in conventions [\#115](https://github.com/devlooped/DependencyInjection/issues/115)
- Detect and report inconsistencies in registration lifetimes [\#134](https://github.com/devlooped/DependencyInjection/pull/134) (@kzu)
- Drastically simplify implementation by removing customization [\#133](https://github.com/devlooped/DependencyInjection/pull/133) (@kzu)
- Allow service key in Service attribute itself [\#131](https://github.com/devlooped/DependencyInjection/pull/131) (@kzu)

:twisted_rightwards_arrows: Merged:

- Update readme.md with new conventions feature [\#132](https://github.com/devlooped/DependencyInjection/pull/132) (@kzu)

## [v2.1.0-rc](https://github.com/devlooped/DependencyInjection/tree/v2.1.0-rc) (2024-12-03)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v2.1.0-beta...v2.1.0-rc)

:sparkles: Implemented enhancements:

- Optimize generator by checking invocations in the predicate [\#124](https://github.com/devlooped/DependencyInjection/pull/124) (@kzu)
- Restore ability to customize namespace and extension class name [\#123](https://github.com/devlooped/DependencyInjection/pull/123) (@kzu)

:bug: Fixed bugs:

- Fix support for multiple \[Export\("contract"\)\] in MEF [\#121](https://github.com/devlooped/DependencyInjection/pull/121) (@kzu)

:twisted_rightwards_arrows: Merged:

- Add SL2 disclaimer to readme and package description [\#129](https://github.com/devlooped/DependencyInjection/pull/129) (@kzu)
- Adopt SponsorLink v2 [\#127](https://github.com/devlooped/DependencyInjection/pull/127) (@kzu)
- Adopt newer .NET branding color since .NET8+ [\#122](https://github.com/devlooped/DependencyInjection/pull/122) (@kzu)

## [v2.1.0-beta](https://github.com/devlooped/DependencyInjection/tree/v2.1.0-beta) (2024-11-12)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v2.0.0...v2.1.0-beta)

:sparkles: Implemented enhancements:

- Provide conventions-based registrations in addition to \[Service\] attributes [\#116](https://github.com/devlooped/DependencyInjection/issues/116)
- Rename project/package to remove the Attributed suffix [\#118](https://github.com/devlooped/DependencyInjection/pull/118) (@kzu)
- Add support for convention-based registrations [\#117](https://github.com/devlooped/DependencyInjection/pull/117) (@kzu)
- Add support for multiple keyed service registrations [\#112](https://github.com/devlooped/DependencyInjection/pull/112) (@kzu)

:bug: Fixed bugs:

- Fix bug in codegen for transient keyed services [\#109](https://github.com/devlooped/DependencyInjection/pull/109) (@kzu)

:hammer: Other:

- \[BREAKING\] In order to support convention registration, generated class and namespace are no longer customizable  [\#119](https://github.com/devlooped/DependencyInjection/issues/119)
- Add support for keyed service with multiple attributes [\#108](https://github.com/devlooped/DependencyInjection/issues/108)

:twisted_rightwards_arrows: Merged:

- Remove mention of ns/class customization, rename class [\#120](https://github.com/devlooped/DependencyInjection/pull/120) (@kzu)
- Add test that showcases internal services can be registered [\#111](https://github.com/devlooped/DependencyInjection/pull/111) (@kzu)

## [v2.0.0](https://github.com/devlooped/DependencyInjection/tree/v2.0.0) (2024-02-29)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v1.3.2...v2.0.0)

:sparkles: Implemented enhancements:

- Add support for new keyed service in .NET8 [\#73](https://github.com/devlooped/DependencyInjection/issues/73)
- Add support for new keyed services in .NET8 [\#90](https://github.com/devlooped/DependencyInjection/pull/90) (@kzu)

:bug: Fixed bugs:

- If compilation isn't successful, incremental generator fails [\#51](https://github.com/devlooped/DependencyInjection/issues/51)

## [v1.3.2](https://github.com/devlooped/DependencyInjection/tree/v1.3.2) (2022-12-13)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v1.3.1...v1.3.2)

:sparkles: Implemented enhancements:

- When registering services, also register Func\<T\> and Lazy\<T\> for services [\#49](https://github.com/devlooped/DependencyInjection/issues/49)

:bug: Fixed bugs:

- Generator shouldn't fail for unsuccessful compilation [\#52](https://github.com/devlooped/DependencyInjection/pull/52) (@kzu)

## [v1.3.1](https://github.com/devlooped/DependencyInjection/tree/v1.3.1) (2022-12-13)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v1.3.0...v1.3.1)

:sparkles: Implemented enhancements:

- Add Func\<T\> and Lazy\<T\> registrations [\#50](https://github.com/devlooped/DependencyInjection/pull/50) (@kzu)

## [v1.3.0](https://github.com/devlooped/DependencyInjection/tree/v1.3.0) (2022-12-06)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v1.2.2...v1.3.0)

:sparkles: Implemented enhancements:

- Make development dependency [\#43](https://github.com/devlooped/DependencyInjection/issues/43)
- Don't report warning for missing AddServices\(\) in test projects [\#48](https://github.com/devlooped/DependencyInjection/pull/48) (@kzu)

:hammer: Other:

- Never report warning for missing AddServices\(\) in test projects [\#47](https://github.com/devlooped/DependencyInjection/issues/47)

## [v1.2.2](https://github.com/devlooped/DependencyInjection/tree/v1.2.2) (2022-11-18)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v1.2.1...v1.2.2)

:sparkles: Implemented enhancements:

- Add support for aliased references [\#40](https://github.com/devlooped/DependencyInjection/issues/40)
- Add support for aliased references [\#41](https://github.com/devlooped/DependencyInjection/pull/41) (@kzu)

:bug: Fixed bugs:

- Don't consider generated code usage of IServiceCollection to report warning [\#37](https://github.com/devlooped/DependencyInjection/issues/37)

## [v1.2.1](https://github.com/devlooped/DependencyInjection/tree/v1.2.1) (2022-11-16)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v1.2.0...v1.2.1)

:sparkles: Implemented enhancements:

- Report warning when using the package without invoking AddServices [\#30](https://github.com/devlooped/DependencyInjection/issues/30)

:twisted_rightwards_arrows: Merged:

- Don't consider generated code usage of IServiceCollection to report warning [\#38](https://github.com/devlooped/DependencyInjection/pull/38) (@kzu)

## [v1.2.0](https://github.com/devlooped/DependencyInjection/tree/v1.2.0) (2022-11-10)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v1.1.3...v1.2.0)

:twisted_rightwards_arrows: Merged:

- Report warning when using the package without invoking AddServices [\#31](https://github.com/devlooped/DependencyInjection/pull/31) (@kzu)

## [v1.1.3](https://github.com/devlooped/DependencyInjection/tree/v1.1.3) (2022-10-31)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v1.1.2...v1.1.3)

:sparkles: Implemented enhancements:

- Optimize incremental codegen by only registering for implementation source generation [\#24](https://github.com/devlooped/DependencyInjection/issues/24)

## [v1.1.2](https://github.com/devlooped/DependencyInjection/tree/v1.1.2) (2022-10-03)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v1.1.1...v1.1.2)

## [v1.1.1](https://github.com/devlooped/DependencyInjection/tree/v1.1.1) (2022-10-03)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v1.1.0...v1.1.1)

:sparkles: Implemented enhancements:

- Add support for MEF attributes [\#17](https://github.com/devlooped/DependencyInjection/issues/17)

## [v1.1.0](https://github.com/devlooped/DependencyInjection/tree/v1.1.0) (2022-09-28)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v1.0.3...v1.1.0)

:sparkles: Implemented enhancements:

- Skip run-time constructor invocation reflection [\#15](https://github.com/devlooped/DependencyInjection/issues/15)
- Replace ServiceAttribute.cs inclusion via targets with source generator-based one [\#12](https://github.com/devlooped/DependencyInjection/issues/12)

:bug: Fixed bugs:

- Library does not work on MAUI [\#13](https://github.com/devlooped/DependencyInjection/issues/13)

:twisted_rightwards_arrows: Merged:

- Add support for MEF attributes [\#18](https://github.com/devlooped/DependencyInjection/pull/18) (@kzu)
- Register implementation factory rather than type [\#16](https://github.com/devlooped/DependencyInjection/pull/16) (@kzu)
- Replace ServiceAttribute inclusion via targets with source generator [\#14](https://github.com/devlooped/DependencyInjection/pull/14) (@kzu)

## [v1.0.3](https://github.com/devlooped/DependencyInjection/tree/v1.0.3) (2022-09-27)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v1.0.2...v1.0.3)

:hammer: Other:

- Make sure package targets netstandard2.0 [\#10](https://github.com/devlooped/DependencyInjection/issues/10)

## [v1.0.2](https://github.com/devlooped/DependencyInjection/tree/v1.0.2) (2022-09-27)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v1.0.1...v1.0.2)

:sparkles: Implemented enhancements:

- Fix transitive contentFiles support that's missing in nuget [\#8](https://github.com/devlooped/DependencyInjection/issues/8)

:twisted_rightwards_arrows: Merged:

- Make sure package targets netstandard2.0 [\#11](https://github.com/devlooped/DependencyInjection/pull/11) (@kzu)

## [v1.0.1](https://github.com/devlooped/DependencyInjection/tree/v1.0.1) (2022-09-27)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v1.0.0...v1.0.1)

:twisted_rightwards_arrows: Merged:

- Fix transitive contentFiles support that's missing in nuget [\#9](https://github.com/devlooped/DependencyInjection/pull/9) (@kzu)

## [v1.0.0](https://github.com/devlooped/DependencyInjection/tree/v1.0.0) (2022-09-26)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/v0.9.0...v1.0.0)

:sparkles: Implemented enhancements:

- Support automatic registration of covariant implemented interfaces [\#6](https://github.com/devlooped/DependencyInjection/issues/6)

:twisted_rightwards_arrows: Merged:

- Support automatic registration of covariant implemented interfaces [\#7](https://github.com/devlooped/DependencyInjection/pull/7) (@kzu)

## [v0.9.0](https://github.com/devlooped/DependencyInjection/tree/v0.9.0) (2022-09-23)

[Full Changelog](https://github.com/devlooped/DependencyInjection/compare/e33ea020586537ad367d7e28fa6503c2f034bf27...v0.9.0)



\* *This Changelog was automatically generated by [github_changelog_generator](https://github.com/github-changelog-generator/github-changelog-generator)*
