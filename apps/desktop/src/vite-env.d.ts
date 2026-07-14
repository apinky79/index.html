interface MarketDnaBridge {
  appName: string;
  appVersion: string;
  initMessage: string;
}

declare global {
  interface Window {
    marketdna?: MarketDnaBridge;
  }
}

export {};
