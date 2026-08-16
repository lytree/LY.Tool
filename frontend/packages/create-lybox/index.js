#!/usr/bin/env node
import { readdir, mkdir, readFile, writeFile } from 'fs/promises';
import { existsSync } from 'fs';
import { join, resolve, dirname } from 'path';
import { fileURLToPath } from 'url';
import { randomUUID } from 'crypto';

const __dirname = dirname(fileURLToPath(import.meta.url));

// 模板 -> 默认项目名 / Mock 端口 / 展示名
const TEMPLATES = {
  react: { dir: 'template-react', defaultName: 'lybox-react-plugin', port: 5173, label: 'React' },
  vue3: { dir: 'template-vue3', defaultName: 'lybox-vue3-plugin', port: 5174, label: 'Vue3' },
};

// 解析命令行参数：首位位置参数为目标目录，--template 指定模板（默认 react）
function parseArgv(argv) {
  const positional = [];
  let template = 'react';
  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--template') {
      template = argv[++i] || 'react';
    } else if (arg.startsWith('--template=')) {
      template = arg.slice('--template='.length);
    } else {
      positional.push(arg);
    }
  }
  if (!TEMPLATES[template]) {
    console.error(`错误：未知模板 "${template}"，可选：${Object.keys(TEMPLATES).join(' | ')}`);
    process.exit(1);
  }
  return { targetDir: positional[0], template };
}

async function copyDir(src, dest, projectName, pluginId) {
  await mkdir(dest, { recursive: true });
  const entries = await readdir(src, { withFileTypes: true });
  for (const entry of entries) {
    const srcPath = join(src, entry.name);
    const destPath = join(dest, entry.name);
    if (entry.isDirectory()) {
      await copyDir(srcPath, destPath, projectName, pluginId);
    } else {
      let content = await readFile(srcPath, 'utf8');
      content = content.replace(/\{\{PROJECT_NAME\}\}/g, projectName);
      content = content.replace(/\{\{PLUGIN_ID\}\}/g, pluginId);
      await writeFile(destPath, content, 'utf8');
    }
  }
}

/**
 * 从共享模板创建 LYBox 插件前端项目。
 *
 * @param {object} options
 * @param {'react'|'vue3'} options.template 模板名称
 * @param {string} [options.targetDir] 目标目录（缺省为当前目录）
 * @param {string} [options.projectName] 项目名（缺省依据模板默认名）
 * @param {string} [options.pluginId] 插件 ID（缺省随机生成）
 */
export async function createFromTemplate({ template, targetDir, projectName, pluginId }) {
  const cfg = TEMPLATES[template];
  if (!cfg) {
    throw new Error(`未知模板 "${template}"，可选：${Object.keys(TEMPLATES).join(' | ')}`);
  }
  const resolvedTarget = targetDir ? resolve(targetDir) : process.cwd();
  const name = projectName || targetDir || cfg.defaultName;
  const pid = pluginId || randomUUID();

  console.log(`\n🚀 创建 LYBox ${cfg.label} 插件: ${name}`);
  console.log(`    模板: ${template}`);
  console.log(`   插件 ID: ${pid}\n`);

  const templateDir = join(__dirname, cfg.dir);
  if (!existsSync(templateDir)) {
    console.error(`错误：找不到 ${cfg.dir} 目录`);
    process.exit(1);
  }

  await copyDir(templateDir, resolvedTarget, name, pid);

  console.log('✅ 项目创建成功！');
  console.log(`\n下一步：`);
  console.log(`  cd ${name}`);
  console.log(`  pnpm install`);
  console.log(`  pnpm dev`);
  console.log(`\n开发时配合 lybox-mock 启动 Mock 后端：`);
  console.log(`  lybox-mock --port ${cfg.port} --wwwroot ./dist\n`);
}

// 直接以 CLI 方式运行时执行（被薄封装 import 时跳过）
const isMainEntry =
  process.argv[1] && resolve(process.argv[1]) === resolve(fileURLToPath(import.meta.url));
if (isMainEntry) {
  const { template, targetDir } = parseArgv(process.argv.slice(2));
  await createFromTemplate({ template, targetDir });
}