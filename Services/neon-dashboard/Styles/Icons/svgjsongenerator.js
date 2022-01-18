/// by: last night
var path = require('path'), fs = require('fs');
/**
 * finds all the svg files from the directory, then generates a json file
 * requires the "code runner" plugin for vs code
 * author: simon
 * @param {*} startPath where to look from
 * @param {*} filter what to look for in filename
 */
function fileToJson(
    filter,
    startPath,
    outPath = ""
) {

    //console.log('Starting from dir '+startPath+'/');

    if (!fs.existsSync(startPath)) {
        console.log("no dir ", startPath);
        return;
    }

    if (outPath == "") {
        outPath = startPath
    }

    var files = fs.readdirSync(startPath);

    var outObj = {
        'svgName': []
    }

    for (var i = 0; i < files.length; i++) {
        var filename = path.join(startPath, files[i]);
        //var stat = fs.lstatSync(filename);

        if (filename.indexOf(filter) >= 0) {

            var name = files[i].replace(filter, '')

            console.log('-- found:', name)

            outObj.svgName.push(name);

            outObj[name] = fs.readFileSync(filename, 'utf8')
        }
    }
    fs.writeFile(path.join(outPath, 'icons.json'), JSON.stringify(outObj), (err) => {
        // throws an error, you could also catch it here
        if (err) throw err;

        // success case, the file was saved
        console.log(' saved!');
    });
}

fileToJson('.svg','./',"../../wwwroot");