const acorn = require('acorn');
const estraverse = require('estraverse');
const fs = require('fs');
const astring = require('astring');

const code = fs.readFileSync('augment-unpacked/extension/out/extension.js', 'utf-8');

const ast = acorn.parse(code, {
    ecmaVersion: 'latest',
    allowHashBang: true,
    sourceType: 'script'
});

// Traverse the AST and replace sensitive requires
const modifiedAst = estraverse.replace(ast, {
    enter: function (node, parent) {
        if (node.type === 'CallExpression' &&
            node.callee.type === 'Identifier' &&
            node.callee.name === 'require' &&
            node.arguments.length > 0 &&
            node.arguments[0].type === 'Literal') {

            const moduleName = node.arguments[0].value;
            if (['crypto', 'http', 'https'].includes(moduleName)) {
                console.log(`[+] Replacing require('${moduleName}') with require('./decoy.js')`);
                node.arguments[0].value = './decoy.js';
            } else if (moduleName === 'vscode') {
                console.log(`[+] Replacing require('vscode') with require('./decoy-vscode.js')`);
                node.arguments[0].value = './decoy-vscode.js';
            }
        }
    }
});

// Generate the modified code
const modifiedCode = astring.generate(modifiedAst);

// Write the modified code to a new file
fs.writeFileSync('augment-unpacked/extension/out/extension.modified.js', modifiedCode);

console.log('\n[+] Successfully created final, neutered extension.modified.js');