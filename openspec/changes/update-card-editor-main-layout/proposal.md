# Change: 更新卡牌编辑器编辑区布局并引入测试页面

## Why
当前卡牌编辑器的编辑区布局需要突出 Preview Art，并将基础信息以更紧凑的方式呈现，减少分区割裂感。同时为了避免直接替换造成视觉偏差，需要先在独立测试页面验证新布局。

## What Changes
- 新增一份与现有样式一致的卡牌编辑器测试页面，用于验证 editor-main 的新布局
- 将 Preview Art 从 Assets 区域移动到 Basic Info，在 editor-main 右侧按原比例展示
- Basic Info 主字段改为紧凑排版：Name 第一行，Cost + Atk + Hp 第二行，Type 第三行，Rarity 第四行，Class 第五行
- Type/Rarity/Class 从下拉菜单改为可选式控件（如按钮/单选式）
- 其余补充信息移动至主信息与图片下方，完整宽度展示

## Impact
- 受影响的 specs: card-editor
- 受影响的代码: src/cardEditor.js, src/styles/card-editor.css, src/router.js, index.html（或对应页面入口）
