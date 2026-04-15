export interface AiProviderInfo {
  provider: string;
  models: string[];
}

export interface AiProvidersResponse {
  defaultProvider: string;
  defaultModel: string;
  providers: AiProviderInfo[];
}
