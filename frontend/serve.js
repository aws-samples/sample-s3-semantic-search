// Cross-platform launcher for the static frontend.
// Reads PORT from environment (set by Aspire) and invokes `serve`.
const { spawn } = require('child_process');
const path = require('path');

const port = process.env.PORT || '3000';
const serveBin = path.resolve(__dirname, 'node_modules', 'serve', 'build', 'main.js');

const child = spawn(
    process.execPath,
    [serveBin, '.', '-l', port, '--no-clipboard', '--cors'],
    { stdio: 'inherit', cwd: __dirname }
);

child.on('exit', (code) => process.exit(code ?? 0));
