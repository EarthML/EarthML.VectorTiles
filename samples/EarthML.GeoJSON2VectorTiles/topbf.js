

var vtpbf = require('vt-pbf')
var fs = require("fs");

module.exports = function (callback, tile,file) {      
    var buff = vtpbf.fromGeojsonVt({ 'geojsonLayer': tile });          
    fs.writeFileSync(file, buff);
    callback(/* error */ null, buff);
};



