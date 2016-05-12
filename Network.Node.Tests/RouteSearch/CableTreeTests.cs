using System;
using Network.Node.Data;
using Network.Node.RouteSearch;
using NUnit.Framework;

namespace Network.Node.Tests.RouteSearch
{
    [TestFixture]
    public class CableTreeTests
    {
        private CableTree _sut;

        [SetUp]
        public void SetUp()
        {
            var clliEntities = new clliEntities();
            //clliEntities.Database.Log = Console.WriteLine;
            _sut = new CableTree(clliEntities);
            _sut.Clear();
        }

        [Test]
        public void ShouldGetAllSegments()
        {
            var segments = _sut.Segments;

            Assert.Greater(segments.Count, 1);
        }

        [Test]
        public void ShouldGetNeighbors()
        {
            var segments = _sut.Segments;

            var neighbors = segments.GetSegmentsFor("AAAAAAAA");

            Assert.Greater(neighbors.Count, 1);
        }

        [Test]
        public void ShouldGetIntermediaryChildren()
        {
            var segments = _sut.Segments;

            var parent = segments.GetSegmentsFor("AAAAAAAA");

            Assert.Greater(parent.Count, 0);
        }
    }
}