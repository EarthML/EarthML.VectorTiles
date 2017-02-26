

var proj4 = require('proj4')

module.exports = function (callback, arg1,arg2,arg3) {
   
   // buff.pipe(callback.stream);
    callback(/* error */ null, proj4(arg1,arg2,arg3));
};



