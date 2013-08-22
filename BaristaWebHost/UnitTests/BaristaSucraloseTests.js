﻿module("Barista Sucralose");

asyncTest("Array - Last", function () {
    var scriptPath = "~/UnitTests/API/Sucralose/array-last.js";

    Barista.runTestScript(scriptPath);
});

asyncTest("Number - Round, Ceil, Floor", function () {
    var scriptPath = "~/UnitTests/API/Sucralose/number-roundceilfloor.js";

    Barista.runTestScript(scriptPath);
});

asyncTest("Object - Merge", function () {
    var scriptPath = "~/UnitTests/API/Sucralose/object-merge.js";

    Barista.runTestScript(scriptPath);
});