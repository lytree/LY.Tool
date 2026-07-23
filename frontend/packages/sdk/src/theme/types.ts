export type ThemeMode = 'light' | 'dark' | 'auto';

export interface DesignTokens {
  color: Record<string, {
    light: { value: string };
    dark: { value: string };
  }>;
  shadow: Record<string, {
    light: { value: string };
    dark: { value: string };
  }>;
  radius: Record<string, { value: string }>;
  spacing: Record<string, { value: string }>;
  fontSize: Record<string, { value: string }>;
  fontFamily: Record<string, { value: string }>;
}
