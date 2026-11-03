# Dopamine.Plugins

Plugin package catalog and bundled plugin assets for EchoStem.

This project is the single home for plugin sources that ship with EchoStem. It
keeps the plugin files out of the application project, copies them to the
application output, and exposes the catalog used by **Settings → Plugins**.

## Layout

```
Dopamine.Plugins/
  PluginManifest.cs          plugin.json contract
  PluginPackage.cs           one discovered package
  PluginCatalogService.cs    scans bundled + user folders
  IPluginCatalogService.cs
  Plugins/
    Dsh/
      EchoStemSync/          EchoStem Now Playing (DSH Web UI)
        plugin.json          EchoStem catalog manifest
        package.json         DSH bundle manifest
        cordis.patch.yml     DSH bundle patch
        index.js             DSH host half
        client.js            DSH browser half (sidebar player)
        locale/              DSH display metadata
        icon.svg
```

At build time the contents of `Plugins/**` are copied to:

- `Dopamine.Plugins\bin\<Configuration>\Plugins`
- `Dopamine\bin\<Configuration>\Plugins` (the running app's catalog root)

The application also scans `%AppData%\Dopamine\Plugins` for user-supplied
packages. A package is a directory whose `plugin.json` declares at least an
`id` or `name`:

```json
{
  "id": "my-plugin",
  "name": "My plugin",
  "version": "1.0.0",
  "kind": "dsh-web",
  "description": "What the plugin adds.",
  "entry": "client.js",
  "host": "index.js"
}
```

Broken manifests are skipped, so one malformed package cannot break the
Plugins page.

## Adding a future plugin

1. Create `Plugins/<Target>/<PluginName>/` in this project.
2. Add a `plugin.json` catalog manifest and the integration's runtime files.
3. Add the package files to the project (wildcards already include
   `Plugins/**/*`; no compile item is needed for data files).
4. Build EchoStem. The package appears in **Settings → Plugins** and is copied
   to the application output.

The Plugins page is read-only by design: installation and activation belong to
the integration that owns the package (for example the DSH Plugin Manager for
`dsh-web` packages). The page provides the catalog, version/kind/description,
the package directory, and an "Open folder" action.
