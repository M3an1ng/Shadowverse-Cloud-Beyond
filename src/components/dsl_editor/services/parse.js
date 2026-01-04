export async function requestParseAstApi(expression) {
    try {
        const res = await fetch("/api/dsl/parse", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ expression })
        });
        if (!res.ok) {
            return {
                success: false,
                message: `解析服务请求失败 (${res.status})`,
                errors: []
            };
        }
        const data = await res.json();
        const errors = data.errors || data.Errors || [];
        const success = Boolean(data.success ?? data.Success);
        return {
            success,
            root: data.root || data.Root || null,
            message: success ? "" : (errors[0] || "解析失败"),
            errors
        };
    } catch (error) {
        return {
            success: false,
            message: "解析服务不可用，已进入原始文本模式。",
            errors: [error?.message || String(error)]
        };
    }
}
