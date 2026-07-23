const Service = require('node-windows').Service;
const path = require('path');

const svc = new Service({
  name: 'CampusPrintAgent',
  script: path.join(__dirname, 'index.js')
});

svc.on('uninstall', function () {
  console.log('\n===============================================================');
  console.log(' SUCCESS: CampusPrint Printer Service Uninstalled Successfully.');
  console.log('===============================================================');
});

svc.uninstall();
