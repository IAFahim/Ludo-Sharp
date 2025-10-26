using System;
using System.Linq;
using NUnit.Framework;
using Ludo;

namespace Ludo.Tests
{
    [TestFixture]
    public class ConstructorAndStateTests
    {
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(5)]
        public void Constructor_InvalidPlayerCount_Throws(int players)
        {
            Assert.Throws<ArgumentException>(() => new LudoBoard(players));
        }

        [TestCase(2, 8)]
        [TestCase(3, 12)]
        [TestCase(4, 16)]
        public void Constructor_InitializesTokensAtBase(int players, int expectedLen)
        {
            var b = new LudoBoard(players);
            Assert.That(b.PlayerCount, Is.EqualTo(players));
            Assert.That(b.TokenPositions.Length, Is.EqualTo(expectedLen));
            Assert.That(b.TokenPositions.All(p => p == LudoBoard.Base), Is.True);
        }
    }

    [TestFixture]
    public class TileTypeTests
    {
        [Test]
        public void TileType_Boundaries()
        {
            var b = new LudoBoard(4);

            b.DebugSetTokenAtRelative(0, 0, LudoBoard.Base);
            Assert.That(b.IsAtBase(b.TokenIndex(0,0)), Is.True);
            Assert.That(b.IsOnMainTrack(b.TokenIndex(0,0)), Is.False);
            Assert.That(b.IsOnHomeStretch(b.TokenIndex(0,0)), Is.False);
            Assert.That(b.IsHome(b.TokenIndex(0,0)), Is.False);

            b.DebugSetTokenAtRelative(0, 0, LudoBoard.MainStart);
            Assert.That(b.IsOnMainTrack(b.TokenIndex(0,0)), Is.True);
            Assert.That(b.IsOnHomeStretch(b.TokenIndex(0,0)), Is.False);
            Assert.That(b.IsHome(b.TokenIndex(0,0)), Is.False);

            b.DebugSetTokenAtRelative(0, 0, LudoBoard.MainEnd);
            Assert.That(b.IsOnMainTrack(b.TokenIndex(0,0)), Is.True);

            b.DebugSetTokenAtRelative(0, 0, LudoBoard.HomeStart);
            Assert.That(b.IsOnHomeStretch(b.TokenIndex(0,0)), Is.True);
            Assert.That(b.IsOnMainTrack(b.TokenIndex(0,0)), Is.False);

            b.DebugSetTokenAtRelative(0, 0, LudoBoard.HomeStart + LudoBoard.StepsToHome - 1); // 58
            Assert.That(b.IsOnHomeStretch(b.TokenIndex(0,0)), Is.True);
            Assert.That(b.IsHome(b.TokenIndex(0,0)), Is.False);

            b.DebugSetTokenAtRelative(0, 0, LudoBoard.Home);
            Assert.That(b.IsHome(b.TokenIndex(0,0)), Is.True);
            Assert.That(b.IsOnHomeStretch(b.TokenIndex(0,0)), Is.False);
            Assert.That(b.IsOnMainTrack(b.TokenIndex(0,0)), Is.False);
        }
    }

    [TestFixture]
    public class BaseExitTests
    {
        [Test]
        public void GetOutOfBase_AllowsExit_WhenStartNotBlockaded()
        {
            var b = new LudoBoard(4);
            b.GetOutOfBase(b.TokenIndex(0,0));
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(LudoBoard.MainStart));
        }

        [Test]
        public void GetOutOfBase_DoesNotCapture_OnSafeStartTile()
        {
            var b = new LudoBoard(4);
            // Opponent on our start (safe)
            b.DebugSetTokenAtAbsolute(1, 0, b.StartAbsoluteTile(0));
            b.GetOutOfBase(b.TokenIndex(0,0));

            Assert.That(b.IsOnMainTrack(b.TokenIndex(1,0)), Is.True); // opponent remains
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(LudoBoard.MainStart));
        }

        [Test]
        public void GetOutOfBase_BlockedByOpponentBlockade_OnStartTile()
        {
            var b = new LudoBoard(4);
            b.DebugMakeBlockadeAtAbsolute(1, b.StartAbsoluteTile(0));
            b.GetOutOfBase(b.TokenIndex(0,0));
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(LudoBoard.Base));
        }

        [Test]
        public void MoveToken_CanExitBase_WithSix_AndNoBlockade()
        {
            var b = new LudoBoard(4);
            b.MoveToken(b.TokenIndex(0,0), LudoBoard.ExitRoll);
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(LudoBoard.MainStart));
        }

        [Test]
        public void MoveToken_CannotExitBase_WhenStartBlockaded()
        {
            var b = new LudoBoard(4);
            b.DebugMakeBlockadeAtAbsolute(2, b.StartAbsoluteTile(0));
            b.MoveToken(b.TokenIndex(0,0), LudoBoard.ExitRoll);
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(LudoBoard.Base));
        }

        [Test]
        public void MoveToken_BaseWithNonSix_DoesNotMove()
        {
            var b = new LudoBoard(4);
            b.MoveToken(b.TokenIndex(0,0), 5);
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(LudoBoard.Base));
        }

        [Test]
        public void ExitBase_NotBlockedByMixedOpponents_OnStartTile()
        {
            var b = new LudoBoard(4);
            int startAbs = b.StartAbsoluteTile(0);
            b.DebugSetTokenAtAbsolute(1, 0, startAbs);
            b.DebugSetTokenAtAbsolute(2, 0, startAbs);

            b.MoveToken(b.TokenIndex(0,0), LudoBoard.ExitRoll);
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(LudoBoard.MainStart));

            // No capture (safe)
            Assert.That(b.IsOnMainTrack(b.TokenIndex(1,0)), Is.True);
            Assert.That(b.IsOnMainTrack(b.TokenIndex(2,0)), Is.True);
        }
    }

    [TestFixture]
    public class MainTrackMovementTests
    {
        [Test]
        public void Move_OnMainTrack_NoWrap_NoHomeEntry()
        {
            var b = new LudoBoard(4);
            b.DebugSetTokenAtRelative(0, 0, 10);
            b.MoveToken(b.TokenIndex(0,0), 3);
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(13));
        }

        [Test]
        public void Move_LandingOn52_StaysOnMainTrack()
        {
            var b = new LudoBoard(4);
            b.DebugSetTokenAtRelative(0, 0, 49);
            b.MoveToken(b.TokenIndex(0,0), 3);
            Assert.That(b.IsOnMainTrack(b.TokenIndex(0,0)), Is.True);
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(52));
        }

        [TestCase(50, 3, 53)]
        [TestCase(51, 2, 53)]
        [TestCase(52, 1, 53)]
        public void EnterHomeStretch_FromDifferentOffsets(int start, int steps, int expected)
        {
            var b = new LudoBoard(4);
            b.DebugSetTokenAtRelative(0, 0, start);
            b.MoveToken(b.TokenIndex(0,0), steps);
            Assert.That(b.IsOnHomeStretch(b.TokenIndex(0,0)), Is.True);
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(expected));
        }

        [TestCase(52, 7, 59)] // exactly home
        [TestCase(55, 4, 59)] // from home stretch to home
        [TestCase(48, 11, 59)] // long exact path to home
        public void ExactToHome_IsAllowed(int start, int steps, int expected)
        {
            var b = new LudoBoard(4);
            b.DebugSetTokenAtRelative(0, 0, start);
            b.MoveToken(b.TokenIndex(0,0), steps);
            Assert.That(b.IsHome(b.TokenIndex(0,0)), Is.True);
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(expected));
        }

        [TestCase(52, 8)]
        [TestCase(58, 2)]
        [TestCase(57, 3)]
        public void OvershootHome_IsIllegal(int start, int steps)
        {
            var b = new LudoBoard(4);
            b.DebugSetTokenAtRelative(0, 0, start);
            b.MoveToken(b.TokenIndex(0,0), steps);
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo((byte)start));
        }

        [Test]
        public void Move_ZeroOrNegativeSteps_DoesNothing()
        {
            var b = new LudoBoard(4);
            b.DebugSetTokenAtRelative(0, 0, 10);
            b.MoveToken(b.TokenIndex(0,0), 0);
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(10));
            b.MoveToken(b.TokenIndex(0,0), -2);
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(10));
        }

        [Test]
        public void PassOverOpponent_NoCapture()
        {
            var b = new LudoBoard(4);
            // Opponent on abs 10
            b.DebugSetTokenAtAbsolute(1, 0, 10);

            // Our token on abs 9 (one step before), move 2 to abs 11
            byte relTo9 = b.RelativeForAbsolute(0, 9);
            b.DebugSetTokenAtRelative(0, 0, relTo9);

            b.MoveToken(b.TokenIndex(0,0), 2);

            // Opponent should remain (not captured)
            Assert.That(b.TokenPositions[b.TokenIndex(1,0)], Is.EqualTo(b.RelativeForAbsolute(1, 10)));
        }
    }

    [TestFixture]
    public class BlockadeTests
    {
        [Test]
        public void PassingOpponentBlockade_IsIllegal()
        {
            var b = new LudoBoard(4);

            // Opponent blockade at abs 20
            b.DebugMakeBlockadeAtAbsolute(1, 20);

            // Choose our start so that next step lands on abs 20
            byte rPlus1 = b.RelativeForAbsolute(0, 20);
            int startRel = rPlus1 - 1;
            if (startRel <= 0) startRel += LudoBoard.MainEnd;

            b.DebugSetTokenAtRelative(0, 0, startRel);
            b.MoveToken(b.TokenIndex(0,0), 1);

            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo((byte)startRel));
        }

        [Test]
        public void OpponentBlockade_OnSafeTile_StillBlocksPassage()
        {
            var b = new LudoBoard(4);
            b.DebugMakeBlockadeAtAbsolute(1, 14);

            byte relTo14 = b.RelativeForAbsolute(0, 14);
            int startRel = relTo14 == 1 ? LudoBoard.MainEnd : relTo14 - 1;
            b.DebugSetTokenAtRelative(0, 0, startRel);

            b.MoveToken(b.TokenIndex(0,0), 1);
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.Not.EqualTo(relTo14));
        }

        [Test]
        public void OwnPair_DoesNotBlock_SelfMovement()
        {
            var b = new LudoBoard(4);
            b.DebugMakeBlockadeAtAbsolute(0, 22);

            byte relTo22 = b.RelativeForAbsolute(0, 22);
            int start = relTo22 == 1 ? LudoBoard.MainEnd : relTo22 - 1;
            b.DebugSetTokenAtRelative(0, 2, start);

            b.MoveToken(b.TokenIndex(0,2), 1);
            Assert.That(b.TokenPositions[b.TokenIndex(0,2)], Is.EqualTo(relTo22));
        }

        [Test]
        public void BlockadeAfterLanding_DoesNotBlockCurrentMove()
        {
            var b = new LudoBoard(4);
            b.DebugMakeBlockadeAtAbsolute(1, 26);

            byte relTo25 = b.RelativeForAbsolute(0, 25);
            int startRel = relTo25 == 1 ? LudoBoard.MainEnd : relTo25 - 1;
            b.DebugSetTokenAtRelative(0, 0, startRel);

            b.MoveToken(b.TokenIndex(0,0), 1);
            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(relTo25));
        }

        [Test]
        public void BlockadeOnHomeEntry_PreventsEnteringHomeStretch()
        {
            var b = new LudoBoard(4);
            // For P0, home entry is absolute 52
            b.DebugMakeBlockadeAtAbsolute(1, 52);

            b.DebugSetTokenAtRelative(0, 0, 50);
            b.MoveToken(b.TokenIndex(0,0), 3);

            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(50));
        }
    }

    [TestFixture]
    public class CaptureTests
    {
        [Test]
        public void LandingOnNonSafe_WithSingleOpponentToken_Captures()
        {
            var b = new LudoBoard(4);
            b.DebugSetTokenAtAbsolute(1, 0, 10);

            byte relTo10 = b.RelativeForAbsolute(0, 10);
            int startRel = relTo10 == 1 ? LudoBoard.MainEnd : relTo10 - 1;
            b.DebugSetTokenAtRelative(0, 0, startRel);

            b.MoveToken(b.TokenIndex(0,0), 1);

            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(relTo10));
            Assert.That(b.TokenPositions[b.TokenIndex(1,0)], Is.EqualTo(LudoBoard.Base));
        }

        [Test]
        public void LandingOnSafe_Tile_DoesNotCapture()
        {
            var b = new LudoBoard(4);
            b.DebugSetTokenAtAbsolute(1, 0, 14);

            byte relTo14 = b.RelativeForAbsolute(0, 14);
            int startRel = relTo14 == 1 ? LudoBoard.MainEnd : relTo14 - 1;
            b.DebugSetTokenAtRelative(0, 0, startRel);

            b.MoveToken(b.TokenIndex(0,0), 1);

            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(relTo14));
            Assert.That(b.IsOnMainTrack(b.TokenIndex(1,0)), Is.True);
            Assert.That(b.TokenPositions[b.TokenIndex(1,0)], Is.EqualTo(b.RelativeForAbsolute(1, 14)));
        }

        [Test]
        public void LandingOnNonSafe_WithTwoOpponentsSameColor_IsIllegal_NoCapture()
        {
            var b = new LudoBoard(4);
            b.DebugMakeBlockadeAtAbsolute(1, 18);

            byte relTo18 = b.RelativeForAbsolute(0, 18);
            int startRel = relTo18 == 1 ? LudoBoard.MainEnd : relTo18 - 1;
            b.DebugSetTokenAtRelative(0, 0, startRel);

            b.MoveToken(b.TokenIndex(0,0), 1);

            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo((byte)startRel));
            Assert.That(b.IsOnMainTrack(b.TokenIndex(1,0)), Is.True);
            Assert.That(b.IsOnMainTrack(b.TokenIndex(1,1)), Is.True);
        }

        [Test]
        public void LandingOnNonSafe_WithTwoOpponentsDifferentColors_CapturesBoth()
        {
            var b = new LudoBoard(4);
            b.DebugSetTokenAtAbsolute(1, 0, 21);
            b.DebugSetTokenAtAbsolute(2, 0, 21);

            byte relTo21 = b.RelativeForAbsolute(0, 21);
            int startRel = relTo21 == 1 ? LudoBoard.MainEnd : relTo21 - 1;
            b.DebugSetTokenAtRelative(0, 0, startRel);

            b.MoveToken(b.TokenIndex(0,0), 1);

            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(relTo21));
            Assert.That(b.TokenPositions[b.TokenIndex(1,0)], Is.EqualTo(LudoBoard.Base));
            Assert.That(b.TokenPositions[b.TokenIndex(2,0)], Is.EqualTo(LudoBoard.Base));
        }

        [Test]
        public void LandingOnOwnToken_IsAllowed_NoCapture()
        {
            var b = new LudoBoard(4);
            b.DebugSetTokenAtAbsolute(0, 1, 30);

            byte relTo30 = b.RelativeForAbsolute(0, 30);
            int startRel = relTo30 == 1 ? LudoBoard.MainEnd : relTo30 - 1;
            b.DebugSetTokenAtRelative(0, 0, startRel);

            b.MoveToken(b.TokenIndex(0,0), 1);

            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(relTo30));
            Assert.That(b.TokenPositions[b.TokenIndex(0,1)], Is.EqualTo(relTo30));
        }
    }

    [TestFixture]
    public class HomeStretchTests
    {
        [Test]
        public void MovingWithinHomeStretch_NoCaptureOccurs()
        {
            var b = new LudoBoard(4);
            b.DebugSetTokenAtAbsolute(1, 0, 12);

            b.DebugSetTokenAtRelative(0, 0, 55);
            b.MoveToken(b.TokenIndex(0,0), 2); // 55 -> 57

            Assert.That(b.TokenPositions[b.TokenIndex(0,0)], Is.EqualTo(57));
            Assert.That(b.TokenPositions[b.TokenIndex(1,0)], Is.EqualTo(b.RelativeForAbsolute(1, 12)));
        }

        [Test]
        public void IsOnSafeTile_TrueThroughoutHomeStretch()
        {
            var b = new LudoBoard(4);
            foreach (var rel in Enumerable.Range(LudoBoard.HomeStart, LudoBoard.StepsToHome))
            {
                b.DebugSetTokenAtRelative(0, 0, rel);
                Assert.That(b.IsOnSafeTile(b.TokenIndex(0,0)), Is.True);
            }
        }
    }

    [TestFixture]
    public class MovableTokensTests
    {
        [Test]
        public void GetMovableTokens_BaseTokensAppearOnlyWithSix_AndWhenNotBlockaded()
        {
            var b = new LudoBoard(4);

            var none = b.GetMovableTokens(0, 5);
            Assert.That(none, Is.Empty);

            var withSix = b.GetMovableTokens(0, LudoBoard.ExitRoll);
            Assert.That(withSix.Count, Is.EqualTo(LudoBoard.Tokens)); // all four can exit

            var b2 = new LudoBoard(4);
            b2.DebugMakeBlockadeAtAbsolute(1, b2.StartAbsoluteTile(0));
            var blocked = b2.GetMovableTokens(0, LudoBoard.ExitRoll);
            Assert.That(blocked, Is.Empty);
        }

        [TestCase(0)]
        [TestCase(7)]
        public void GetMovableTokens_IgnoresInvalidDice(int dice)
        {
            var b = new LudoBoard(4);
            Assert.That(b.GetMovableTokens(0, dice), Is.Empty);
        }
    }

    [TestFixture]
    public class HasWonTests
    {
        [Test]
        public void HasWon_TrueOnlyWhenAllTokensHome()
        {
            var b = new LudoBoard(4);
            for (int i = 0; i < 3; i++)
                b.DebugSetTokenAtRelative(0, i, LudoBoard.Home);
            b.DebugSetTokenAtRelative(0, 3, LudoBoard.Home - 1);

            Assert.That(b.HasWon(0), Is.False);

            b.DebugSetTokenAtRelative(0, 3, LudoBoard.Home);
            Assert.That(b.HasWon(0), Is.True);
        }
    }

    [TestFixture]
    public class ErrorHandlingTests
    {
        [Test]
        public void MoveToken_InvalidTokenIndex_Throws()
        {
            var b = new LudoBoard(4);
            Assert.Throws<ArgumentOutOfRangeException>(() => b.MoveToken(-1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.MoveToken(b.TokenPositions.Length, 1));
        }

        [Test]
        public void GetOutOfBase_InvalidTokenIndex_Throws()
        {
            var b = new LudoBoard(4);
            Assert.Throws<ArgumentOutOfRangeException>(() => b.GetOutOfBase(-5));
            Assert.Throws<ArgumentOutOfRangeException>(() => b.GetOutOfBase(b.TokenPositions.Length));
        }
    }
    
}