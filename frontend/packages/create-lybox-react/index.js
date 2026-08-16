#!/usr/bin/env node
/**
 * create-lybox-react —— 薄封装：固定使用 React 模板。
 * 共享逻辑见 ../create-lybox/index.js。
 */
import { createFromTemplate } from '../create-lybox/index.js';

const targetDir = process.argv[2];
await createFromTemplate({ template: 'react', targetDir });