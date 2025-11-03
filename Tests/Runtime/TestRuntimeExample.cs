// NUnit test suite for LudoBoard (Try* pattern edition)
// Edge-cases galore, arranged with AAA, sprinkled with a little joy ✨

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Ludo;

namespace Ludo.Tests
{
    [TestFixture]
    [TestOf(typeof(LudoBoard))]
    public class LudoBoard_TryPattern_Tests
    {
        private const int Players4 = 4;
        private const int Players2 = 2;
        private const byte Base = 0;
        private const byte Start = 1;
        private const byte TotalMain = 52; // must align with implementation
        private const byte HomeStretchStartExpected = 52; // per user directive; do NOT change logic here
        private const byte StepsToHome = 6;
        private const byte HomeExpected = (byte)(HomeStretchStartExpected + StepsToHome); // 58

        private LudoBoard _board4;
        private LudoBoard _board2;

        [SetUp]
        public void SetUp()
        {
            _board4 = new LudoBoard(Players4);
            _board2 = new LudoBoard(Players2);
        }

        // ---------- Helpers ----------
        private static int Idx(int player, int token) => player * 4 + token; // 4 tokens per player

        private static int GetPlayerOffset(int player, int playerCount)
        {
            // Mirrors production logic
            int perQuarter = TotalMain / 4; // 13
            return playerCount == 2 ? player * 2 * perQuarter : player * perQuarter;
        }

        private static byte RelativeForAbsolute(int absolute, int player, int playerCount)
        {
            // invert ToAbsoluteMainTrack: (r-1+offset)%52 + 1 = absolute
            int offset = GetPlayerOffset(player, playerCount);
            int r0 = ((absolute - 1 - offset) % TotalMain + TotalMain) % TotalMain; // 0..51
            return (byte)(r0 + 1);
        }

        private static void PlaceBlockadeAtAbsolute(ref LudoBoard b, int absolute, (int p, int t) a, (int p, int t) c)
        {
            // two tokens on same absolute tile (any colors)
            var r1 = RelativeForAbsolute(absolute, a.p, b.playerCount);
            var r2 = RelativeForAbsolute(absolute, c.p, b.playerCount);
            Assert.That(b.TrySetTokenPosition(Idx(a.p, a.t), r1, out _));
            Assert.That(b.TrySetTokenPosition(Idx(c.p, c.t), r2, out _));
        }

        private static void SetRelative(ref LudoBoard b, int player, int token, byte relative)
        {
            Assert.That(b.TrySetTokenPosition(Idx(player, token), relative, out _));
        }

        private static void SetHome(ref LudoBoard b, int player, int token)
        {
            // 58 given HomeStretchStart=52 and StepsToHome=6
            byte home = (byte)(HomeStretchStartExpected + StepsToHome);
            Assert.That(b.TrySetTokenPosition(Idx(player, token), home, out _));
        }

        // ---------- Structural / invariant tests ----------

        [Test]
        public void Constructor_Initializes_AllTokensAtBase()
        {
            // Arrange + Act (done in SetUp)

            // Assert
            for (int i = 0; i < _board4.tokenPositions.Length; i++)
                Assert.That(_board4.tokenPositions[i], Is.EqualTo(Base));
        }

        [Test]
        public void HomeStretch_StartsAt_52_AndOverlapsMainTrack()
        {
            // Arrange
            var idx = Idx(0, 0);
            Assert.That(_board4.TrySetTokenPosition(idx, HomeStretchStartExpected, out _));

            // Assert: at 52 we should be BOTH on main track and home stretch per current design
            Assert.Multiple(() =>
            {
                Assert.That(_board4.IsOnHomeStretch(idx), Is.True);
                Assert.That(_board4.IsOnMainTrack(idx), Is.True);
            });
        }

        // ---------- TryGet/SetTokenPosition ----------

        [Test]
        public void TryGetTokenPosition_InvalidIndex_Fails()
        {
            Assert.That(_board4.TryGetTokenPosition(-1, out _, out var e1), Is.False);
            Assert.That(e1, Is.EqualTo(LudoError.InvalidTokenIndex));

            Assert.That(_board4.TryGetTokenPosition(_board4.tokenPositions.Length, out _, out var e2), Is.False);
            Assert.That(e2, Is.EqualTo(LudoError.InvalidTokenIndex));
        }

        [Test]
        public void TrySetTokenPosition_InvalidIndex_Fails()
        {
            Assert.That(_board4.TrySetTokenPosition(-1, 1, out var e1), Is.False);
            Assert.That(e1, Is.EqualTo(LudoError.InvalidTokenIndex));

            Assert.That(_board4.TrySetTokenPosition(_board4.tokenPositions.Length, 1, out var e2), Is.False);
            Assert.That(e2, Is.EqualTo(LudoError.InvalidTokenIndex));
        }

        // ---------- TryHasWon ----------

        [Test]
        public void TryHasWon_InvalidPlayer_Fails()
        {
            Assert.That(_board4.TryHasWon(-1, out _, out var e1), Is.False);
            Assert.That(e1, Is.EqualTo(LudoError.InvalidPlayerIndex));

            Assert.That(_board4.TryHasWon(_board4.playerCount, out _, out var e2), Is.False);
            Assert.That(e2, Is.EqualTo(LudoError.InvalidPlayerIndex));
        }

        [Test]
        public void TryHasWon_AllTokensHome_ReturnsTrue()
        {
            for (int t = 0; t < 4; t++) SetHome(ref _board4, 1, t);
            Assert.That(_board4.TryHasWon(1, out var won, out var err), Is.True);
            Assert.That(err, Is.EqualTo(default(LudoError)));
            Assert.That(won, Is.True);
        }

        [Test]
        public void TryHasWon_NotAllTokensHome_ReturnsFalse()
        {
            for (int t = 0; t < 3; t++) SetHome(ref _board4, 1, t);
            Assert.That(_board4.TryHasWon(1, out var won, out _), Is.True);
            Assert.That(won, Is.False);
        }

        // ---------- TryGetMovableTokens ----------

        [TestCase(-1, 6, LudoError.InvalidPlayerIndex)]
        [TestCase(99, 6, LudoError.InvalidPlayerIndex)]
        [TestCase(0, 0, LudoError.InvalidDiceRoll)]
        [TestCase(0, 7, LudoError.InvalidDiceRoll)]
        public void TryGetMovableTokens_InvalidInputs_Fail(int player, int dice, LudoError expected)
        {
            Assert.That(_board4.TryGetMovableTokens(player, dice, out _, out var err), Is.False);
            Assert.That(err, Is.EqualTo(expected));
        }

        [Test]
        public void TryGetMovableTokens_BaseWithSix_YieldsTokensUnlessStartBlocked()
        {
            // Arrange: everyone at base; for player 0 a and b; blockade on their start (absolute 1)
            // Use two opponent tokens to make blockade
            PlaceBlockadeAtAbsolute(ref _board4, absolute: 1, a: (1, 0), c: (2, 0));

            // Act + Assert
            Assert.That(_board4.TryGetMovableTokens(0, 6, out var movable, out var err), Is.True);
            Assert.That(err, Is.EqualTo(default(LudoError)));

            // Because start is blocked, nothing should be movable from base
            Assert.That(movable, Is.Empty);
        }

        // ---------- TryGetOutOfBase ----------

        [Test]
        public void TryGetOutOfBase_NotAtBase_Fails()
        {
            var idx = Idx(0, 0);
            SetRelative(ref _board4, 0, 0, 5);
            Assert.That(_board4.TryGetOutOfBase(idx, out var err), Is.False);
            Assert.That(err, Is.EqualTo(LudoError.TokenNotAtBase));
        }

        [Test]
        public void TryGetOutOfBase_BlockedStart_Fails()
        {
            // Block absolute 1 (player 0 start) with two tokens
            PlaceBlockadeAtAbsolute(ref _board4, absolute: 1, a: (1, 0), c: (2, 0));

            var idx = Idx(0, 0);
            Assert.That(_board4.TryGetOutOfBase(idx, out var err), Is.False);
            Assert.That(err, Is.EqualTo(LudoError.PathBlocked));
        }

        [Test]
        public void TryGetOutOfBase_Succeeds_SetsStartPosition()
        {
            var idx = Idx(0, 0);
            Assert.That(_board4.TryGetOutOfBase(idx, out var err), Is.True);
            Assert.That(err, Is.EqualTo(default(LudoError)));

            Assert.That(_board4.TryGetTokenPosition(idx, out var pos, out _), Is.True);
            Assert.That(pos, Is.EqualTo(Start));
        }

        // ---------- TryMoveToken (base / invalid) ----------

        [Test]
        public void TryMoveToken_InvalidIndexOrStepsOrAlreadyHome_Fails()
        {
            Assert.That(_board4.TryMoveToken(-1, 1, out _, out var e1), Is.False);
            Assert.That(e1, Is.EqualTo(LudoError.InvalidTokenIndex));

            var idx = Idx(0, 0);
            SetHome(ref _board4, 0, 0);
            Assert.That(_board4.TryMoveToken(idx, 1, out _, out var e2), Is.False);
            Assert.That(e2, Is.EqualTo(LudoError.TokenAlreadyHome));

            // reset
            _board4 = new LudoBoard(Players4);
            Assert.That(_board4.TryMoveToken(Idx(0,0), 0, out _, out var e3), Is.False);
            Assert.That(e3, Is.EqualTo(LudoError.InvalidDiceRoll));
        }

        [Test]
        public void TryMoveToken_FromBaseWithNonSix_Fails()
        {
            var idx = Idx(0, 0);
            Assert.That(_board4.TryMoveToken(idx, 5, out _, out var err), Is.False);
            Assert.That(err, Is.EqualTo(LudoError.TokenNotMovable));
        }

        [Test]
        public void TryMoveToken_FromBaseWithSix_WhenBlocked_Fails()
        {
            PlaceBlockadeAtAbsolute(ref _board4, absolute: 1, a: (1, 0), c: (2, 0));
            var idx = Idx(0, 0);
            Assert.That(_board4.TryMoveToken(idx, 6, out _, out var err), Is.False);
            Assert.That(err, Is.EqualTo(LudoError.PathBlocked));
        }

        [Test]
        public void TryMoveToken_FromBaseWithSix_WhenFree_Succeeds()
        {
            var idx = Idx(0, 0);
            Assert.That(_board4.TryMoveToken(idx, 6, out var newPos, out var err), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(err, Is.EqualTo(default(LudoError)));
                Assert.That(newPos, Is.EqualTo(Start));
            });
        }

        // ---------- TryMoveToken (path blocking & capture) ----------

        [Test]
        public void TryMoveToken_PathBlocked_Midway_Fails()
        {
            // Put our token at relative 1 (abs 1), attempt to move 5 steps to 6; blockade at absolute 3
            SetRelative(ref _board4, 0, 0, 1);
            PlaceBlockadeAtAbsolute(ref _board4, absolute: 3, a: (1, 0), c: (2, 0));

            Assert.That(_board4.TryMoveToken(Idx(0,0), 5, out _, out var err), Is.False);
            Assert.That(err, Is.EqualTo(LudoError.PathBlocked));
        }

        [Test]
        public void TryMoveToken_CaptureOpponentOnUnsafe_SendsOpponentToBase()
        {
            // Our token at 1 -> move 2 to 3. Place opponent single token at absolute 3 (unsafe)
            SetRelative(ref _board4, 0, 0, 1);
            var oppIdx = Idx(1, 0);
            var oppRel = RelativeForAbsolute(3, 1, _board4.playerCount);
            SetRelative(ref _board4, 1, 0, oppRel);

            Assert.That(_board4.TryMoveToken(Idx(0,0), 2, out var newPos, out var err), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(err, Is.EqualTo(default(LudoError)));
                Assert.That(newPos, Is.EqualTo(3));
            });

            // Opponent should be bonked back to base
            Assert.That(_board4.TryGetTokenPosition(oppIdx, out var oppNow, out _), Is.True);
            Assert.That(oppNow, Is.EqualTo(Base));
        }

        [Test]
        public void TryMoveToken_LandsOnSafeTile_NoCapture()
        {
            // Safe absolute tiles include 1,14,27,40. We'll collide on 27 (player 1 start), which is safe
            SetRelative(ref _board4, 0, 0, 14); // For player 0, relative 14 => absolute 14? (offset 0) yes

            // Place opponent at absolute 27
            var oppIdx = Idx(1, 0);
            var oppRel = RelativeForAbsolute(27, 1, _board4.playerCount);
            SetRelative(ref _board4, 1, 0, oppRel);

            // March our token around to absolute 27: need delta of 13 from abs 14
            // So 13 steps => relative target 27 (still <= 52)
            Assert.That(_board4.TryMoveToken(Idx(0,0), 13, out var pos, out var err), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(err, Is.EqualTo(default));
                Assert.That(pos, Is.EqualTo(27));
            });

            // Opponent should remain on tile (safe tile)
            Assert.That(_board4.TryGetTokenPosition(oppIdx, out var oppNow, out _), Is.True);
            Assert.That(oppNow, Is.EqualTo(oppRel));
        }

        // ---------- TryMoveToken (home stretch rules with start at 52) ----------

        [Test]
        public void TryMoveToken_FromNearEnd_EnterHome_Exactly()
        {
            // Place our token at relative 52 (which is also home-stretch per current design)
            SetRelative(ref _board4, 0, 0, HomeStretchStartExpected);
            Assert.That(_board4.TryMoveToken(Idx(0,0), StepsToHome, out var pos, out var err), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(err, Is.EqualTo(default));
                Assert.That(pos, Is.EqualTo(HomeExpected)); // 58
            });
        }

        [Test]
        public void TryMoveToken_FromHomeStretch_WouldOvershoot_Fails()
        {
            // Set to 57 then try to move 3 -> would be 60 > 58
            SetRelative(ref _board4, 0, 0, (byte)(HomeExpected - 1));
            Assert.That(_board4.TryMoveToken(Idx(0,0), 3, out _, out var err), Is.False);
            Assert.That(err, Is.EqualTo(LudoError.WouldOvershootHome));
        }

        [Test]
        public void TryMoveToken_OnMainTrack_PassingBeyond52_TransitionsToHomeStretchStartingAt52()
        {
            // From 51 with roll 2 -> relativeTarget 53 => home-stretch tile = 52
            SetRelative(ref _board4, 0, 0, 51);
            Assert.That(_board4.TryMoveToken(Idx(0,0), 2, out var pos, out var err), Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(err, Is.EqualTo(default));
                Assert.That(pos, Is.EqualTo(HomeStretchStartExpected)); // 52
                Assert.That(_board4.IsOnHomeStretch(Idx(0,0)), Is.True);
                Assert.That(_board4.IsOnMainTrack(Idx(0,0)), Is.True); // overlapping zone by design
            });
        }

        // ---------- Offsets in 2-player mode ----------

        [Test]
        public void TwoPlayer_Offsets_AreOpposite_0_and_26()
        {
            // player 0 start absolute should be 1; player 1 start absolute should be 27 (safe tile)
            Assert.That(_board2.TryGetOutOfBase(Idx(0,0), out _), Is.True);
            Assert.That(_board2.TryGetOutOfBase(Idx(1,0), out _), Is.True);

            // We can't query absolute directly (private); but we can test via capture attempt:
            // Move p0 26 steps to try to land on p1's start (27). Since it's safe, it won't capture.
            Assert.That(_board2.TryMoveToken(Idx(0,0), 26, out var p0pos, out _), Is.True);
            // Put a second token of p1 on start to verify it's still there (no capture)
            Assert.That(_board2.TryGetOutOfBase(Idx(1,1), out _), Is.True);

            // If safe worked, both tokens co-exist and NO capture occurred
            Assert.That(_board2.TryGetTokenPosition(Idx(1,0), out var p1start, out _));
            Assert.That(_board2.TryGetTokenPosition(Idx(1,1), out var p1start2, out _));
            Assert.Multiple(() =>
            {
                Assert.That(p0pos, Is.EqualTo(27));
                Assert.That(p1start, Is.EqualTo(1)); // relative 1 for player 1 = absolute 27
                Assert.That(p1start2, Is.EqualTo(1));
            });
        }

        // ---------- Legacy wrapper sanity ----------

        [Test]
        public void HasWon_Legacy_InvalidPlayer_ReturnsFalse()
        {
            Assert.That(_board4.HasWon_Legacy(-1), Is.False);
            Assert.That(_board4.HasWon_Legacy(_board4.playerCount), Is.False);
        }
    }
}
