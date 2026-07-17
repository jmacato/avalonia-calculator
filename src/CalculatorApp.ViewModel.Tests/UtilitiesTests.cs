using CalculatorApp.ViewModel.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CalculatorApp.ViewModel.Tests
{
    [TestClass]
    public class UtilitiesTests
    {
        [TestMethod]
        public void NoCoreWindowOnThreadReturnsNegativeOne()
        {
            Assert.AreEqual(-1, Utilities.GetWindowId());
        }
    }
}
