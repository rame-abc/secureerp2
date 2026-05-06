using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SecureERP2.Tests.Finance
{
    [TestClass]
    public class SimpleTest
    {
        [TestMethod]
        public void SimpleTestMethod_ShouldPass()
        {
            // Arrange
            var expected = true;
            
            // Act
            var actual = true;
            
            // Assert
            Assert.AreEqual(expected, actual);
        }
    }
}
