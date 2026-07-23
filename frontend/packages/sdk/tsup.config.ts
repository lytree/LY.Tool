import { defineConfig } from 'tsup';

export default defineConfig({
  // 主入口 + theme 子入口（对应 package.json exports 的 "./theme"）
  entry: ['src/index.ts', 'src/theme/index.ts'],
  format: ['esm', 'cjs'],
  dts: true,
  splitting: false,
  sourcemap: true,
  clean: true,
  treeshake: true,
  minify: false,
  // CSS 与 tokens.json 由 package.json 的 exports 直接指向 src，无需 tsup 处理
});
