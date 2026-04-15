export interface UserInfo {
  id: string;
  email: string;
  displayName: string;
}

export interface TokenPair {
  accessToken: string;
  accessTokenExpiresAt: string;
  user: UserInfo;
}
