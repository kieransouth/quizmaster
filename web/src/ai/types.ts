export interface AiProviderInfo {
  provider: string;
  models: string[];
}

export interface AiProvidersResponse {
  defaultProvider: string;
  defaultModel: string;
  providers: AiProviderInfo[];
}

export interface UserApiKeyStatus {
  provider: string;
  hasKey: boolean;
  /** Last few characters of the stored key, or null when unset. */
  masked: string | null;
}
