const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');
const source = fs.readFileSync(path.join(__dirname, '../Assets/ShipMayham/Runtime/WebCursor.jslib'), 'utf8');

function install(request) {
    const warnings = [];
    const canvas = { requestPointerLock: request };
    const library = {};
    vm.runInNewContext(source, {
        Module: { canvas }, LibraryManager: { library },
        mergeInto: Object.assign, console: { warn: (...args) => warnings.push(args) }
    });
    library.ShipCursor_Install();
    return { canvas, warnings, library };
}

(async () => {
    let called = 0;
    const normal = install(function (options) {
        called++;
        assert.equal(this, normal.canvas);
        assert.equal(options.token, 123);
        return Promise.resolve('locked');
    });
    const wrapper = normal.canvas.requestPointerLock;
    normal.library.ShipCursor_Install();
    assert.equal(normal.canvas.requestPointerLock, wrapper, 'Scene reload must not stack wrappers.');
    assert.equal(await normal.canvas.requestPointerLock({ token: 123 }), 'locked');
    assert.equal(called, 1);
    assert.equal(normal.warnings.length, 0);

    const denied = install(() => Promise.reject(new Error('native pointer lock failure')));
    assert.equal(await denied.canvas.requestPointerLock(), undefined);
    assert.equal(denied.warnings.length, 1);
    assert.match(denied.warnings[0][0], /Click the game center/);

    const synchronous = install(() => { throw new Error('unsupported gesture'); });
    assert.equal(synchronous.canvas.requestPointerLock(), undefined);
    assert.equal(synchronous.warnings.length, 1);

    const legacy = install(() => undefined);
    assert.equal(legacy.canvas.requestPointerLock(), undefined);
    assert.equal(legacy.warnings.length, 0);
    install(undefined).library.ShipCursor_Install();
    console.log('SHIP_WEB_CURSOR_TEST_PASS — success, async rejection, sync failure, legacy and idempotence.');
})().catch(error => { console.error(error); process.exitCode = 1; });
