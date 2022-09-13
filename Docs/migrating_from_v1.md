# Migrating from DarkConfig v1

DarkConfig v2 introduces some breaking API changes. This is a quick guide on migrating from v1

- `DarkConfig.Config` is now `DarkConfig.Configs`
- `Config.Load()` is now `Configs.ParseFile()`
- `ConfigReifier.Reify()` is now `Configs.Reify()`
- 2D arrays are transposed compared to pre-migration. See: https://github.com/SpryFox/DarkConfig/issues/28
- `DocNode` methods `AsInt()`, `AsFloat()`, `AsString()`, `AsBool()` have been removed. You can either add your own extension methods or change to use `As<>()`
- `index.bytes` is not needed for `FileSource`
- `ConfigFileInfo.Size` is now 64-bits (`long`)
- Logs require the define flag `DC_LOGGING_ENABLED`

