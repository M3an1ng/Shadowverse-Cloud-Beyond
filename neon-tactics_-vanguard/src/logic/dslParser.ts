
/**
 * VBA Migration: SplitEffect
 * Original File: `·4·7·辅助·SplitEffect.bas`
 * 
 * Splits an effect string by "||", but respects nested curly braces "{}".
 * Example: "Fanfare||Give|It|{LastWords||Draw|1}" -> ["Fanfare", "Give|It|{LastWords||Draw|1}"]
 */
export const splitEffect = (inputString: string): string[] => {
    const results: string[] = [];
    let currentPart = "";
    let braceLevel = 0;
    let i = 0;

    while (i < inputString.length) {
        const char = inputString[i];
        const nextChar = inputString[i + 1];

        // Handle Braces nesting
        if (char === '{') {
            braceLevel++;
            currentPart += char;
        } else if (char === '}') {
            if (braceLevel > 0) braceLevel--;
            currentPart += char;
        } 
        // Handle Separator "||" ONLY if not inside braces
        else if (braceLevel === 0 && char === '|' && nextChar === '|') {
            if (currentPart.length > 0) {
                results.push(currentPart);
            }
            currentPart = "";
            i++; // Skip the second '|'
        } else {
            currentPart += char;
        }
        i++;
    }

    if (currentPart.length > 0) {
        results.push(currentPart);
    }

    return results;
};

/**
 * Helper to parse a command string like "Damage|Enemy-Follower|3"
 * into parts ["Damage", "Enemy-Follower", "3"]
 */
export const parseCommand = (commandStr: string): string[] => {
    // Simple split by '|', but we might need to respect nested structures later.
    // For now, assuming commands inside top-level segments don't have complex nesting that breaks '|' splitting 
    // unless they are wrapped in {}.
    // The VBA logic mostly relies on `Split(Str, "|")` for sub-commands.
    return commandStr.split('|');
};
