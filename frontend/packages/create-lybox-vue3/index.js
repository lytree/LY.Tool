#!/usr/bin/env node
/**
 * create-lybox-vue3 —— 薄封装：固定使用 Vue3 模板。
 * 共享逻辑见 ../create-lybox/index.js。
 */
import { createFromTemplate } from '../create-lybox/index.js';

const targetDir = process.argv[2];
await createFromTemplate({ template: 'vue3', targetDir });