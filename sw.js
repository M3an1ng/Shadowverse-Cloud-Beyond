// 后端从工作区根目录提供入口；Vite 构建直接发布 public/sw.js。
// 两条部署路径共用同一套 worker 实现。
importScripts('/public/sw.js');
