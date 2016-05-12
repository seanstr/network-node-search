using System;
using System.Linq;
using Network.Node.RouteSearch;
using NUnit.Framework;

namespace Network.Node.Tests.RouteSearch
{
    [TestFixture]
    public class CableRouteSearchTest
    {
        private CableRouteSearch _search;

        [SetUp]
        public void Setup()
        {
            _search = new CableRouteSearch();
        }

        [Test]
        public void ShouldFindRouteBetweenJunctions()
        {
            var results = _search.Search("AAAAAAAA", "AAAAAAAA").ToList();
            Assert.AreEqual("AAAAAAAA", results.First().START_CUST_SITE_CLLI);
            Assert.AreEqual("AAAAAAAA", results.Last().START_CUST_SITE_CLLI);
        }

        [Test]
        public void ShouldFindReverseRoute()
        {
            var results = _search.Search("AAAAAAAA", "AAAAAAAA").ToList();
            Assert.AreEqual("AAAAAAAA", results.First().START_CUST_SITE_CLLI);
            Assert.AreEqual("AAAAAAAA", results.Last().START_CUST_SITE_CLLI);
        }

        [Test]
        public void ShouldFindRouteBetweenPaths()
        {
            var results = _search.Search("AAAAAAAA", "AAAAAAAA").ToList();
            Assert.AreEqual("AAAAAAAA", results.First().END_CUST_SITE_CLLI);
            Assert.AreEqual("AAAAAAAA", results.Last().START_CUST_SITE_CLLI);
        }

        [Test]
        public void ShouldFindRouteAdjacentPaths()
        {
            var results = _search.Search("AAAAAAAA", "AAAAAAAA").ToList();
            Assert.AreEqual("AAAAAAAA", results.First().END_CUST_SITE_CLLI);
            Assert.AreEqual("AAAAAAAA", results.Last().START_CUST_SITE_CLLI);
        }

        [Test]
        public void ShouldHandleParallelCables()
        {
            var results = _search.Search("AAAAAAAA", "AAAAAAAA").ToList();
            Assert.AreEqual("AAAAAAAA", results.First().END_CUST_SITE_CLLI);
            Assert.AreEqual("AAAAAAAA", results.Last().START_CUST_SITE_CLLI);
        }

        [Test]
        public void ShouldHandleGuysTest()
        {
            var results = _search.Search("AAAAAAAA", "AAAAAAAA").ToList();
            Assert.AreEqual("AAAAAAAA", results.First().START_CUST_SITE_CLLI);
            Assert.AreEqual("AAAAAAAA", results.Last().START_CUST_SITE_CLLI);
        }

        [Test]
        public void ShouldHandleLongTest()
        {
            var results = _search.Search("AAAAAAAA", "AAAAAAAA").ToList();
            Assert.AreEqual("AAAAAAAA", results.First().END_CUST_SITE_CLLI);
            Assert.AreEqual("AAAAAAAA", results.Last().START_CUST_SITE_CLLI);
        }

        [Test]
        [Ignore("Ignore a test")]
        public void ShouldHandleAbFttpTest()
        {
            var results = _search.Search("AAAAAAAA", "AAAAAAAA").ToList();
            Assert.AreEqual("AAAAAAAA", results.First().START_CUST_SITE_CLLI);
            Assert.AreEqual("AAAAAAAA", results.Last().END_CUST_SITE_CLLI);
        }

        [Test]
        public void ShouldHandleNonMeanderingTests()
        {
            var results = _search.Search("AAAAAAAA", "AAAAAAAA").ToList();
            Assert.Less(results.Count, 1000);
            Assert.AreEqual("AAAAAAAA", results.First().END_CUST_SITE_CLLI);
            Assert.AreEqual("AAAAAAAA", results.Last().START_CUST_SITE_CLLI);
        }

        [Test]
        public void ShouldHandleNonMeanderingTests2()
        {
            var results = _search.Search("AAAAAAAA", "AAAAAAAA").ToList();
            Assert.Less(results.Count, 1000);
            Assert.AreEqual("AAAAAAAA", results.First().END_CUST_SITE_CLLI);
            Assert.AreEqual("AAAAAAAA", results.Last().START_CUST_SITE_CLLI);
        }

        [Test]
        public void ShouldHandleSimpleSearch_TP12885()
        {
            var results = _search.Search("AAAAAAAA", "AAAAAAAA").ToList();
            Assert.AreEqual("AAAAAAAA", results.First().END_CUST_SITE_CLLI);
            Assert.AreEqual("AAAAAAAA", results.Last().END_CUST_SITE_CLLI);
        }

        [Test]
        public void ShouldHandleSimpleSearch_TP12877()
        {
            var results = _search.Search("AAAAAAAA", "AAAAAAAA").ToList();
            Assert.AreEqual("AAAAAAAA", results.First().END_CUST_SITE_CLLI);
            Assert.AreEqual("AAAAAAAA", results.Last().END_CUST_SITE_CLLI);
        }

        [Test]
        public void ShouldHandleSimpleSearch_TP12888()
        {
            var results = _search.Search("AAAAAAAA", "AAAAAAAA").ToList();
            Assert.AreEqual("AAAAAAAA", results.First().END_CUST_SITE_CLLI);
            Assert.AreEqual("AAAAAAAA", results.Last().END_CUST_SITE_CLLI);
        }

        [Test]
        public void ShouldHandleSimpleSearch_Guy1()
        {
            var results = _search.Search("AAAAAAAA", "AAAAAAAA").ToList();
            Assert.AreEqual("AAAAAAAA", results.First().END_CUST_SITE_CLLI);
            Assert.AreEqual("AAAAAAAA", results.Last().END_CUST_SITE_CLLI);
        }

        [Test]
        public void ShouldHandleSimpleSearch_Guy2()
        {
            var results = _search.Search("AAAAAAAA", "AAAAAAAA").ToList();
            Assert.AreEqual("AAAAAAAA", results.First().END_CUST_SITE_CLLI);
            Assert.AreEqual("AAAAAAAA", results.Last().START_CUST_SITE_CLLI);
        }
    }
}