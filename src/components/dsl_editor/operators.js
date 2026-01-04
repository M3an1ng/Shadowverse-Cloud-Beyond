// Phase B modularization - operators/options utilities (partial migration)

// 为减少侵入，本模块先提供 getOptionsForType(ctx, typeName) 的实现，
// 其余函数将在后续步骤逐步迁移。

/**
 * 根据类型返回可用的操作项（操作符/函数/查询）
 * ctx 为 DslEditor 实例，用于调用现有的工具方法
 */
export function getOptionsForType(ctx, typeName) {
  let options = [];

  // 数值类型：提供数值操作符和函数
  if (ctx.isNumericType(typeName)) {
    const smartOperators = ctx.getSmartOperators(typeName) || [];
    options.push(...smartOperators);

    // 数值函数（来自 DSL 元数据）
    const functions = ctx.metadata?.dsl?.functions || [];
    const numericFunctions = functions
      .filter((f) => ctx.isFunctionCompatibleWithType(f, typeName))
      .map((f) => ({
        type: "function",
        value: f.name,
        label: f.name,
        description: f.description,
        returnType: f.returnType || f.returnTypeName || 'Object',
        meta: f
      }));
    options.push(...numericFunctions);
  }

  // 文本类型（包括枚举和GUID）：提供文本操作符和函数
  else if (ctx.isTextType(typeName)) {
    const textOperators = ctx.getTextOperators() || [];
    options.push(...textOperators);

    const textFunctions = ctx.getTextFunctions() || [];
    options.push(...textFunctions);
  }

  // 布尔类型：提供布尔操作符
  else if (typeName === 'Boolean') {
    const smartOperators = ctx.getSmartOperators(typeName) || [];
    options.push(...smartOperators);
  }

  // 集合类型：提供集合函数和查询操作
  else if (ctx.isCollectionType(typeName)) {
    options.push(...getCollectionOperators());

    const collectionFunctions = ctx.getCollectionFunctions() || [];
    options.push(...collectionFunctions);

    options.push({
      type: "listPipeline",
      value: "pipeline",
      label: "筛选",
      description: "打开筛选/排序/选择管线"
    });
  }

  // 对象类型：提供属性访问或通用比较操作符
  else if (ctx.isObjectType(typeName)) {
    if (typeName === 'Object') {
      options.push(
        { type: "operator", value: "==", label: "等于 (==)", description: "相等比较" },
        { type: "operator", value: "!=", label: "不等于 (!=)", description: "不等比较" }
      );
    } else {
      const objectProperties = ctx.getObjectProperties(typeName) || [];
      options.push(...objectProperties);
    }
  }

  return options;
}

// 迁移 getSmartOperators / getTextOperators / getEnumOperators 到模块

export function getSmartOperators(ctx, typeName) {
  const operators = [];

  if (ctx.isNumericType(typeName)) {
    operators.push(
      { type: "operator", value: "+", label: "加法 (+)", description: "数值加法运算", priority: 110 },
      { type: "operator", value: "-", label: "减法 (-)", description: "数值减法运算", priority: 110 },
      { type: "operator", value: "*", label: "乘法 (*)", description: "数值乘法运算", priority: 115 },
      { type: "operator", value: "/", label: "除法 (/)", description: "数值除法运算", priority: 115 },

      { type: "operator", value: "==", label: "等于 (==)", description: "数值相等比较", priority: 100 },
      { type: "operator", value: "!=", label: "不等于 (!=)", description: "数值不等比较", priority: 95 },
      { type: "operator", value: ">", label: "大于 (>)", description: "数值大小比较", priority: 90 },
      { type: "operator", value: ">=", label: "大于等于 (>=)", description: "数值大小比较", priority: 85 },
      { type: "operator", value: "<", label: "小于 (<)", description: "数值大小比较", priority: 90 },
      { type: "operator", value: "<=", label: "小于等于 (<=)", description: "数值大小比较", priority: 85 }
    );
  } else if (typeName === 'Boolean') {
    operators.push(
      { type: "operator", value: "==", label: "等于 (==)", description: "布尔值相等比较", priority: 100 },
      { type: "operator", value: "!=", label: "不等于 (!=)", description: "布尔值不等比较", priority: 95 },
      { type: "operator", value: "&&", label: "逻辑与 (&&)", description: "逻辑AND操作", priority: 80 },
      { type: "operator", value: "||", label: "逻辑或 (||)", description: "逻辑OR操作", priority: 75 }
    );
  } else if (ctx.isTextType(typeName)) {
    operators.push(
      { type: "operator", value: "==", label: "等于 (==)", description: "文本相等比较", priority: 100 },
      { type: "operator", value: "!=", label: "不等于 (!=)", description: "文本不等比较", priority: 95 }
    );
    if (typeName === 'String') {
      operators.push(
        { type: "operator", value: "contains", label: "包含", description: "字符串包含检查", priority: 85 },
        { type: "operator", value: "startsWith", label: "开始于", description: "字符串前缀检查", priority: 80 },
        { type: "operator", value: "endsWith", label: "结束于", description: "字符串后缀检查", priority: 80 }
      );
    }
  } else if (ctx.isBusinessObjectType(typeName)) {
    operators.push(
      { type: "operator", value: "==", label: "等于 (==)", description: "对象相等比较", priority: 100 },
      { type: "operator", value: "!=", label: "不等于 (!=)", description: "对象不等比较", priority: 95 }
    );
  } else {
    operators.push(
      { type: "operator", value: "==", label: "等于 (==)", description: "相等比较", priority: 100 },
      { type: "operator", value: "!=", label: "不等于 (!=)", description: "不等比较", priority: 95 }
    );
  }

  return operators
    .sort((a, b) => b.priority - a.priority)
    .map(op => ({ type: op.type, value: op.value, label: op.label, description: op.description }));
}

export function getEnumOperators(ctx, typeName) {
  const operators = [];
  operators.push(
    { type: "operator", value: "==", label: "等于 (==)", description: "枚举值相等比较", priority: 100 },
    { type: "operator", value: "!=", label: "不等于 (!=)", description: "枚举值不等比较", priority: 95 }
  );
  const enumValues = ctx.getEnumValues(typeName) || [];
  if (enumValues.length > 2) {
    operators.push(
      { type: "operator", value: "in", label: "属于 (in)", description: "检查是否在指定值列表中", priority: 85 },
      { type: "operator", value: "notIn", label: "不属于 (notIn)", description: "检查是否不在指定值列表中", priority: 80 }
    );
  }
  return operators
    .sort((a, b) => b.priority - a.priority)
    .map(op => ({ type: op.type, value: op.value, label: op.label, description: op.description }));
}

export function getTextOperators() {
  return [
    { type: "operator", value: "==", label: "等于 (==)", description: "文本相等比较", priority: 100 },
    { type: "operator", value: "!=", label: "不等于 (!=)", description: "文本不等比较", priority: 95 }
  ].sort((a, b) => b.priority - a.priority)
   .map(op => ({ type: op.type, value: op.value, label: op.label, description: op.description }));
}

function getCollectionOperators() {
  return [
    { type: "operator", value: "|", label: "并集 (|)", description: "合并两个集合并去重" },
    { type: "operator", value: "&", label: "交集 (&)", description: "取两个集合的交集" },
    { type: "operator", value: "-", label: "差集 (-)", description: "从左侧集合中排除右侧集合" }
  ];
}
