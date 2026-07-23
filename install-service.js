const Service = require('node-windows').Service;
const path = require('path');

const svc = new Service({
  name: 'CampusPrintAgent',
  description: 'CampusPrint Automatic Background Printer Agent Service',
  script: path.join(__dirname, 'index.js'),
  wait: 2,
  grow: 0.25,
  maxRestarts: 10
});

svc.on('install', function () {
  console.log('\n=============================================================');
  console.log(' SUCCESS: CampusPrint Printer Service Installed Successfully!');
  console.log('=============================================================');
  console.log('Starting service now...');
  svc.start();
});

svc.on('alreadyinstalled', function () {
  console.log('\n[INFO] CampusPrint Printer Service is already installed.');
  console.log('Starting service...');
  svc.start();
});

svc.on('start', function () {
  console.log('[RUNNING] CampusPrint Service is active in the background.');
  console.log('It will automatically start after every Windows reboot.\n');
});

svc.on('error', function (err) {
  console.error('[ERROR] Service error:', err);
});

svc.install();
