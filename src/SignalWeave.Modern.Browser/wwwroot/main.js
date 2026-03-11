const assetVersion = '911a25e';
const { dotnet } = await import(`./_framework/dotnet.js?v=${assetVersion}`);

const isBrowser = typeof window !== 'undefined';
if (!isBrowser) {
  throw new Error('Expected to be running in a browser');
}

const dotnetRuntime = await dotnet
  .withDiagnosticTracing(false)
  .withApplicationArgumentsFromQuery()
  .withOnConfigLoaded(config => {
    config.cacheBootResources = false;
    config.disableNoCacheFetch = true;
  })
  .withModuleConfig({
    loadBootResource: (_type, _name, defaultUri) =>
      `${defaultUri}${defaultUri.includes('?') ? '&' : '?'}v=${assetVersion}`
  })
  .create();

const config = dotnetRuntime.getConfig();
await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
