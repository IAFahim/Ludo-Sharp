using System;
using System.Collections.Generic;

namespace Ludo
{
    // Error types for Ludo game
    public enum LudoError
    {
        InvalidTokenIndex,
        TokenNotMovable,
        InvalidDiceRoll,
        TokenAlreadyHome,
        TokenNotAtBase,
        PathBlocked,
        WouldOvershootHome,
        InvalidPlayerIndex
    }

    public struct LudoBoard
    {
        private const byte BasePosition = 0;
        private const byte StartPosition = 1;
        private const byte TotalMainTrackTiles = 52;
        private const byte HomeStretchStartPosition = 52;
        public const byte StepsToHome = 6;
        private const byte HomePosition = HomeStretchStartPosition + StepsToHome; // 58
        private const byte ExitFromBaseAtRoll = 6;
        private const byte TokensPerPlayer = 4;
        private const byte PlayerTrackOffset = TotalMainTrackTiles / 4; // 13

        // Absolute safe tiles (relative 1 per player). These are globally safe (no captures here).
        public static readonly byte[] SafeAbsoluteTiles = new byte[] { 1, 14, 27, 40 };

        public byte[] tokenPositions;
        public readonly int playerCount;

        // =========================
        // 🎯 NEW Try* Public API
        // =========================

        /// <summary>
        /// Tries to get a token's position.
        /// Success? ✅ position is set. Failure? ❌ error explains why.
        /// </summary>
        public bool TryGetTokenPosition(int tokenIndex, out byte position, out LudoError error)
        {
            if (!IsValidTokenIndex(tokenIndex))
            {
                position = default;
                error = LudoError.InvalidTokenIndex;
                return false;
            }

            position = tokenPositions[tokenIndex];
            error = default;
            return true;
        }

        /// <summary>
        /// Tries to set a token's position. (Useful for tests/debugging.)
        /// </summary>
        public bool TrySetTokenPosition(int tokenIndex, byte position, out LudoError error)
        {
            if (!IsValidTokenIndex(tokenIndex))
            {
                error = LudoError.InvalidTokenIndex;
                return false;
            }

            tokenPositions[tokenIndex] = position;
            error = default;
            return true;
        }

        /// <summary>
        /// Tries to evaluate if <paramref name="playerIndex"/> has won.
        /// </summary>
        public bool TryHasWon(int playerIndex, out bool hasWon, out LudoError error)
        {
            if (playerIndex < 0 || playerIndex >= playerCount)
            {
                hasWon = default;
                error = LudoError.InvalidPlayerIndex;
                return false;
            }

            var start = playerIndex * TokensPerPlayer;
            for (int i = 0; i < TokensPerPlayer; i++)
            {
                if (!IsHome(start + i))
                {
                    hasWon = false;
                    error = default;
                    return true;
                }
            }

            hasWon = true;
            error = default;
            return true;
        }

        /// <summary>
        /// Tries to list movable tokens for a player given a dice roll.
        /// The list can be empty on success if nothing can move.
        /// </summary>
        public bool TryGetMovableTokens(int playerIndex, int diceRoll, out List<int> movableTokens, out LudoError error)
        {
            if (playerIndex < 0 || playerIndex >= playerCount)
            {
                movableTokens = default!;
                error = LudoError.InvalidPlayerIndex;
                return false;
            }

            if (diceRoll < 1 || diceRoll > 6)
            {
                movableTokens = default!;
                error = LudoError.InvalidDiceRoll;
                return false;
            }

            movableTokens = new List<int>();
            int playerTokenStartIndex = playerIndex * TokensPerPlayer;

            for (int i = 0; i < TokensPerPlayer; i++)
            {
                int tokenIndex = playerTokenStartIndex + i;
                if (TryComputeNewPosition(tokenIndex, diceRoll, out _, out _))
                {
                    movableTokens.Add(tokenIndex);
                }
            }

            error = default;
            return true;
        }

        /// <summary>
        /// Tries to take a token out of base (requires a 6 and a non-blocked start tile).
        /// </summary>
        public bool TryGetOutOfBase(int tokenIndex, out LudoError error)
        {
            // This is a pure action (no new position returned), so we only expose the error as out.
            if (!IsValidTokenIndex(tokenIndex))
            {
                error = LudoError.InvalidTokenIndex;
                return false;
            }

            if (!IsAtBase(tokenIndex))
            {
                error = LudoError.TokenNotAtBase;
                return false;
            }

            int playerIndex = tokenIndex / TokensPerPlayer;
            int startAbsolute = ToAbsoluteMainTrack(StartPosition, playerIndex);

            if (IsTileBlocked(startAbsolute))
            {
                error = LudoError.PathBlocked;
                return false;
            }

            tokenPositions[tokenIndex] = StartPosition;
            error = default;
            return true;
        }

        /// <summary>
        /// Tries to move a token forward by <paramref name="steps"/>.
        /// On success, <paramref name="newPosition"/> is the new board-relative position (0=base, 1..52 main, 53..58 stretch, 59=home).
        /// </summary>
        public bool TryMoveToken(int tokenIndex, int steps, out byte newPosition, out LudoError error)
        {
            newPosition = default;

            if (!IsValidTokenIndex(tokenIndex))
            {
                error = LudoError.InvalidTokenIndex;
                return false;
            }

            if (steps <= 0)
            {
                error = LudoError.InvalidDiceRoll;
                return false;
            }

            if (IsHome(tokenIndex))
            {
                error = LudoError.TokenAlreadyHome;
                return false;
            }

            if (!TryComputeNewPosition(tokenIndex, steps, out var target, out error))
            {
                return false;
            }

            tokenPositions[tokenIndex] = target;

            // 🔥 Capture time (only if we end on a non-safe main track tile)
            if (IsOnMainTrack(tokenIndex) && !IsOnSafeTile(tokenIndex))
            {
                CaptureTokensAt(tokenIndex);
            }

            newPosition = target;
            return true;
        }

        // =========================
        // 🏗️ Struct plumbing
        // =========================

        public LudoBoard(int numberOfPlayers)
        {
            playerCount = numberOfPlayers;
            tokenPositions = new byte[playerCount * TokensPerPlayer]; // default 0 = base
        }

        public bool IsAtBase(int tokenIndex) => tokenPositions[tokenIndex] == BasePosition;

        public bool IsOnMainTrack(int tokenIndex) => tokenPositions[tokenIndex] >= StartPosition &&
                                                     tokenPositions[tokenIndex] <= TotalMainTrackTiles;

        public bool IsOnHomeStretch(int tokenIndex) => tokenPositions[tokenIndex] >= HomeStretchStartPosition &&
                                                       tokenPositions[tokenIndex] < HomePosition;

        public bool IsHome(int tokenIndex) => tokenPositions[tokenIndex] == HomePosition;

        public bool IsOnSafeTile(int tokenIndex)
        {
            if (IsOnHomeStretch(tokenIndex)) return true; // your cozy hallway to 🏠
            if (!IsOnMainTrack(tokenIndex)) return false;

            var absolutePosition = GetAbsolutePosition(tokenIndex);
            return IsSafeAbsoluteTile((byte)absolutePosition);
        }

        private static bool IsSafeAbsoluteTile(byte absolute)
        {
            // small & fast, no LINQ required
            for (int i = 0; i < SafeAbsoluteTiles.Length; i++)
                if (SafeAbsoluteTiles[i] == absolute) return true;
            return false;
        }

        // =========================
        // 🧭 Movement core (Try-compute only)
        // =========================

        /// <summary>
        /// Brain of the move: figures out where a token would land and whether the path is blocked.
        /// </summary>
        private bool TryComputeNewPosition(int tokenIndex, int steps, out byte newPosition, out LudoError error)
        {
            byte currentPosition = tokenPositions[tokenIndex];

            if (IsHome(tokenIndex))
            {
                newPosition = default;
                error = LudoError.TokenAlreadyHome;
                return false;
            }

            int playerIndex = tokenIndex / TokensPerPlayer;

            if (IsAtBase(tokenIndex))
            {
                if (steps != ExitFromBaseAtRoll)
                {
                    newPosition = default;
                    error = LudoError.TokenNotMovable;
                    return false;
                }

                int startAbs = ToAbsoluteMainTrack(StartPosition, playerIndex);
                if (IsTileBlocked(startAbs))
                {
                    newPosition = default;
                    error = LudoError.PathBlocked;
                    return false;
                }

                newPosition = StartPosition;
                error = default;
                return true;
            }

            if (IsOnMainTrack(tokenIndex))
            {
                int relativeTarget = currentPosition + steps;

                // 🚧 Check each main-track step for blockades (two or more tokens of ANY color)
                int stepsOnTrack = Math.Min(steps, TotalMainTrackTiles - currentPosition);
                for (int i = 1; i <= stepsOnTrack; i++)
                {
                    byte nextRelative = (byte)(currentPosition + i);
                    int nextAbsolute = ToAbsoluteMainTrack(nextRelative, playerIndex);
                    if (IsTileBlocked(nextAbsolute))
                    {
                        newPosition = default;
                        error = LudoError.PathBlocked;
                        return false;
                    }
                }

                if (relativeTarget <= TotalMainTrackTiles)
                {
                    newPosition = (byte)relativeTarget;
                    error = default;
                    return true;
                }

                int stepsIntoHome = relativeTarget - TotalMainTrackTiles; // 1..(StepsToHome+?)
                int target = HomeStretchStartPosition + stepsIntoHome - 1; // 53..59

                if (target > HomePosition)
                {
                    newPosition = default;
                    error = LudoError.WouldOvershootHome;
                    return false;
                }

                newPosition = (byte)target;
                error = default;
                return true;
            }

            if (IsOnHomeStretch(tokenIndex))
            {
                int target = currentPosition + steps;
                if (target > HomePosition)
                {
                    newPosition = default;
                    error = LudoError.WouldOvershootHome;
                    return false;
                }

                newPosition = (byte)target;
                error = default;
                return true;
            }

            newPosition = default;
            error = LudoError.TokenNotMovable;
            return false;
        }

        private void CaptureTokensAt(int movedTokenIndex)
        {
            if (!IsOnMainTrack(movedTokenIndex)) return;
            if (IsOnSafeTile(movedTokenIndex)) return;

            int movedTokenPlayerIndex = movedTokenIndex / TokensPerPlayer;
            int newAbsolutePosition = GetAbsolutePosition(movedTokenIndex);

            for (int i = 0; i < tokenPositions.Length; i++)
            {
                if (movedTokenPlayerIndex == (i / TokensPerPlayer)) continue; // not your own crew
                if (!IsOnMainTrack(i)) continue;

                int opponentAbsolutePosition = GetAbsolutePosition(i);
                if (newAbsolutePosition == opponentAbsolutePosition)
                {
                    tokenPositions[i] = BasePosition; // bonk! back to base 💥
                }
            }
        }

        /// <summary>
        /// A tile is blocked if it contains TWO OR MORE tokens, regardless of color.
        /// This matches common Ludo "blockade" rules and prevents passing through.
        /// </summary>
        private bool IsTileBlocked(int absolutePosition)
        {
            int countOnTile = 0;
            for (int i = 0; i < tokenPositions.Length; i++)
            {
                if (IsOnMainTrack(i) && GetAbsolutePosition(i) == absolutePosition)
                {
                    countOnTile++;
                    if (countOnTile >= 2) return true; // 🚧 blockade!
                }
            }

            return false;
        }

        private int GetAbsolutePosition(int tokenIndex)
        {
            if (!IsOnMainTrack(tokenIndex)) return -1;

            int playerIndex = tokenIndex / TokensPerPlayer;
            int relativePosition = tokenPositions[tokenIndex];
            int playerOffset = GetPlayerTrackOffset(playerIndex);

            int absolutePosition = (relativePosition - 1 + playerOffset) % TotalMainTrackTiles + 1;
            return absolutePosition;
        }

        private int ToAbsoluteMainTrack(byte relativeMainTrackTile, int playerIndex)
        {
            int playerOffset = GetPlayerTrackOffset(playerIndex);
            return (relativeMainTrackTile - 1 + playerOffset) % TotalMainTrackTiles + 1;
        }

        private int GetPlayerTrackOffset(int playerIndex)
        {
            // In 2-player mode, start spots are opposite (0 and 26)
            if (playerCount == 2) return playerIndex * 2 * PlayerTrackOffset;
            return playerIndex * PlayerTrackOffset;
        }

        private byte GetHomeEntryTile(int playerIndex)
        {
            int playerOffset = GetPlayerTrackOffset(playerIndex);
            if (playerOffset == 0) return TotalMainTrackTiles;
            return (byte)playerOffset;
        }

        private bool IsValidTokenIndex(int tokenIndex)
        {
            return tokenIndex >= 0 && tokenIndex < tokenPositions.Length;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append('P').Append(playerCount).Append(" | ");
            for (int p = 0; p < playerCount; p++)
            {
                if (p > 0) sb.Append(" || ");
                sb.Append('p').Append(p).Append(':');

                int start = p * TokensPerPlayer;
                for (int t = 0; t < TokensPerPlayer; t++)
                {
                    if (t > 0) sb.Append(',');
                    int idx = start + t;
                    byte pos = tokenPositions[idx];

                    if (IsAtBase(idx))
                    {
                        sb.Append('B');
                        continue;
                    }

                    if (IsHome(idx))
                    {
                        sb.Append('H');
                        continue;
                    }

                    if (IsOnHomeStretch(idx))
                    {
                        int step = pos - HomeStretchStartPosition + 1; // 1..StepsToHome
                        sb.Append('S').Append(step);
                        if (IsOnSafeTile(idx)) sb.Append('*');
                        continue;
                    }

                    // On main track
                    int abs = GetAbsolutePosition(idx);
                    sb.Append(pos).Append('@').Append(abs);
                    if (IsOnSafeTile(idx)) sb.Append('*');
                }
            }

            return sb.ToString();
        }
    }
}
