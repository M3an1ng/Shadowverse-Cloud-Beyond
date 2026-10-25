const TYPE_ALIASES = {
    bool: "Boolean",
    boolean: "Boolean",
    "system.boolean": "Boolean",
    string: "String",
    "system.string": "String",
    object: "Object",
    "system.object": "Object",
    guid: "Guid",
    "system.guid": "Guid",
    int: "Int32",
    int32: "Int32",
    "system.int32": "Int32",
    long: "Int64",
    int64: "Int64",
    "system.int64": "Int64",
    float: "Single",
    single: "Single",
    "system.single": "Single",
    double: "Double",
    "system.double": "Double",
    decimal: "Decimal",
    "system.decimal": "Decimal",
    number: "Number"
};

const SEMANTIC_TYPE_ALIASES = {
    ...TYPE_ALIASES,
    cardinstancesnapshot: "CardInstance",
    cardinstanceresetsnapshot: "CardInstance",
    carddefinitionsnapshot: "CardDefinition"
};

// 显示保留快照类型名；语义判断将快照视为对应领域类型。
export function normalizeDisplayTypeName(typeName) {
    return normalizeWithAliases(typeName, TYPE_ALIASES);
}

export function normalizeSemanticTypeName(typeName) {
    return normalizeWithAliases(typeName, SEMANTIC_TYPE_ALIASES);
}

function splitGenericTypeArguments(raw) {
    const text = String(raw || "").trim();
    if (!text) return [];
    const result = [];
    let depth = 0;
    let current = "";
    for (let i = 0; i < text.length; i++) {
        const ch = text[i];
        if (ch === "<") {
            depth += 1;
            current += ch;
            continue;
        }
        if (ch === ">") {
            depth = Math.max(0, depth - 1);
            current += ch;
            continue;
        }
        if (ch === "," && depth === 0) {
            const token = current.trim();
            if (token) result.push(token);
            current = "";
            continue;
        }
        current += ch;
    }
    const tail = current.trim();
    if (tail) result.push(tail);
    return result;
}

function normalizeWithAliases(typeName, aliasMap) {
    if (!typeName) return null;
    const trimmed = String(typeName).trim().replace(/^global::/i, "");
    if (!trimmed) return null;

    const lower = trimmed.toLowerCase();
    if (Object.prototype.hasOwnProperty.call(aliasMap, lower)) {
        return aliasMap[lower];
    }

    const nullable = trimmed.match(/^Nullable<(.+)>$/i);
    if (nullable) {
        return normalizeWithAliases(nullable[1], aliasMap) || nullable[1].trim();
    }

    const genericMatch = trimmed.match(/^([^<]+)<(.+)>$/);
    if (genericMatch) {
        const containerRaw = genericMatch[1].trim();
        const containerLower = containerRaw.toLowerCase();
        const rawArgs = splitGenericTypeArguments(genericMatch[2]);
        const normalizedArgs = rawArgs.map((arg) => normalizeWithAliases(arg, aliasMap) || arg.trim());

        if (["list", "ienumerable", "ireadonlylist", "ireadonlycollection", "icollection"].includes(containerLower)) {
            const inner = normalizedArgs[0] || "Object";
            return `List<${inner}>`;
        }

        if (["dictionary", "idictionary", "ireadonlydictionary"].includes(containerLower)) {
            const keyType = normalizedArgs[0] || "Object";
            const valueType = normalizedArgs[1] || "Object";
            const dictionaryNameMap = {
                dictionary: "Dictionary",
                idictionary: "IDictionary",
                ireadonlydictionary: "IReadOnlyDictionary"
            };
            const name = dictionaryNameMap[containerLower] || containerRaw;
            return `${name}<${keyType}, ${valueType}>`;
        }

        return `${containerRaw}<${normalizedArgs.join(", ")}>`;
    }

    if (["list", "ienumerable", "ireadonlylist", "ireadonlycollection", "icollection"].includes(lower)) {
        return "List<Object>";
    }

    return trimmed;
}
