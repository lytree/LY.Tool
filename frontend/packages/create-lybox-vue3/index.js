#!/usr/bin/env node
import { readdir, mkdir, copyFile, stat, readFile, writeFile } from 'fs/promises';
import { existsSync } from 'fs';
import { join, resolve, dirname } from 'path';
import { fileURLToPath } from 'url';
import { randomUUID } from 'crypto';

const __dirname = dirname(fileURLToPath(import.meta.url));
const templateDir = join(__dirname, 'template');

// 解析目标目录
const targetDir = process.argv[2] ? resolve(process.argv[2]) : process.cwd();
const projectName = process.argv[2] || 'lybox-vue3-plugin';
const pluginId = randomUUID();

console.log(`\n🚀 创建 LYBox Vue3 插件: ${projectName}`);
console.log(`   插件 ID: ${pluginId}\n`);

async function copyDir(src, dest) {
  await mkdir(dest, { recursive: true });
  const entries = await readdir(src, { withFileTypes: true });
  for (const entry of entries) {
    const srcPath = join(src, entry.name);
    const destPath = join(dest, entry.name);
    if (entry.isDirectory()) {
      await copyDir(srcPath, destPath);
    } else {
      let content = await readFile(srcPath, 'utf8');
      // 替换占位符
      content = content.replace(/\{\{PROJECT_NAME\}\}/g, projectName);
      content = content.replace(/\{\{PLUGIN_ID\}\}/g, pluginId);
      await writeFile(destPath, content, 'utf8');
    }
  }
}

if (!existsSync(templateDir)) {
  console.error('错误：找不到 template 目录');
  process.exit(1);
}

await copyDir(templateDir, targetDir);

console.log('✅ 项目创建成功！');
console.log(`\n下一步：`);
console.log(`  cd ${projectName}`);
console.log(`  pnpm install`);
console.log(`  pnpm dev`);
console.log(`\n开发时配合 lybox-mock 启动 Mock 后端：`);
console.log(`  lybox-mock --port 5174 --wwwroot ./dist\n`);
