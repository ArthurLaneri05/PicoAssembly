using PicoAssembly.Test;
using PicoAssembly.Test.Integration.CachedObjectTests;
using PicoAssembly.Test.Unit.HashingTests;
using PicoAssembly.Test.Unit.StoragePathsTests;

var suite = new TestSuite();
HashingTests.Run(suite);
StoragePathsTests.Run(suite);
CachedObjectTests.Run(suite);
return suite.Finish();
