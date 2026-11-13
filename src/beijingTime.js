const beijingTime = new Intl.DateTimeFormat('zh-CN', {
    timeZone: 'Asia/Shanghai',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
    hourCycle: 'h23'
});

export function formatBeijingTime(value, fallback = '-') {
    if (!value) return fallback;
    // SQLite timestamps written in UTC can be serialized without an offset.
    const timestamp = /^\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d+)?$/.test(value)
        ? value.replace(' ', 'T') + 'Z'
        : value;
    const date = new Date(timestamp);
    return Number.isNaN(date.getTime()) ? fallback : beijingTime.format(date);
}
