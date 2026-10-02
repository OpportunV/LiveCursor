using NUnit.Framework;
using Opportunv.LiveCursor.Editor;
using UnityEngine;

namespace Opportunv.LiveCursor.Tests.Editor
{
    public sealed class CursorFrameScalerTests
    {
        [Test]
        public void Scale_SameSizeReturnsCopy()
        {
            var source = new[] { new Color32(1, 2, 3, 4) };

            var result = CursorFrameScaler.Scale(source, 1, 1, 1, 1);

            Assert.That(result, Is.EqualTo(source));
            Assert.That(result, Is.Not.SameAs(source));
        }

        [Test]
        public void Scale_AveragesOpaquePixelsInLinearLight()
        {
            var source = new[] { new Color32(0, 0, 0, 255), new Color32(255, 255, 255, 255) };

            var result = CursorFrameScaler.Scale(source, 2, 1, 1, 1);

            Assert.That(result[0].r, Is.InRange(186, 190));
            Assert.That(result[0].a, Is.EqualTo(255));
        }

        [Test]
        public void Scale_TransparentNeighbourDoesNotDarkenColour()
        {
            var source = new[] { new Color32(255, 0, 0, 255), new Color32(0, 0, 0, 0) };

            var result = CursorFrameScaler.Scale(source, 2, 1, 1, 1);

            Assert.That(result[0].r, Is.EqualTo(255));
            Assert.That(result[0].a, Is.InRange(127, 128));
        }

        [Test]
        public void Scale_FullyTransparentStaysClear()
        {
            var source = new Color32[16];

            var result = CursorFrameScaler.Scale(source, 4, 4, 2, 2);

            Assert.That(result, Has.All.EqualTo(new Color32(0, 0, 0, 0)));
        }

        [Test]
        public void Scale_NonIntegerRatioKeepsSolidColour()
        {
            var source = new Color32[128 * 128];
            for (var i = 0; i < source.Length; i++)
            {
                source[i] = new(10, 200, 30, 255);
            }

            var result = CursorFrameScaler.Scale(source, 128, 128, 48, 48);

            Assert.That(result, Has.All.EqualTo(new Color32(10, 200, 30, 255)));
        }

        [Test]
        public void NaturalComparer_OrdersNumbersByValue()
        {
            var files = new[] { "Frame_10.png", "Frame_2.png", "Frame_001.png" };

            System.Array.Sort(files, NaturalStringComparer.Instance);

            Assert.That(files, Is.EqualTo(new[] { "Frame_001.png", "Frame_2.png", "Frame_10.png" }));
        }
    }
}
