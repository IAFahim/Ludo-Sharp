using System;
using System.Collections.Generic;

namespace Ludo
{
    [Serializable]
    public struct LudoBoard : ILudoBoard
    {
        // ===== Constants =====
        private const byte BasePosition = 0;
        private const byte StartPosition = 1;
        private const byte TotalMainTrackTiles = 52;          // 1..52
        private const byte HomeStretchStartPosition = 53;     // 53..58
        public  const byte StepsToHome = 6;
        private const byte HomePosition = HomeStretchStartPosition + StepsToHome; // 59
        private const byte ExitFromBaseAtRoll = 6;
        private const byte TokensPerPlayer = 4;
        private const byte PlayerTrackOffset = TotalMainTrackTiles / 4; // 13

        public const byte Base = BasePosition;
        public const byte MainStart = StartPosition;
        public const byte MainEnd = TotalMainTrackTiles;
        public const byte HomeStart = HomeStretchStartPosition;
        public const byte Home = HomePosition;
        public const byte ExitRoll = ExitFromBaseAtRoll;
        public const byte Tokens = TokensPerPlayer;

        public static readonly byte[] SafeAbsoluteTiles = {1, 14, 27, 40};

        public int playerCount;
        public byte[] tokenPositions;

        public LudoBoard(int numberOfPlayers)
        {
            playerCount = numberOfPlayers;
            tokenPositions = new byte[playerCount * TokensPerPlayer];
        }
        
        public byte[] TokenPositions => tokenPositions;
        public int PlayerCount => playerCount;
        public bool IsAtBase(int t) => tokenPositions[t] == BasePosition;
        public bool IsOnMainTrack(int t) => tokenPositions[t] >= StartPosition && tokenPositions[t] <= TotalMainTrackTiles;
        public bool IsOnHomeStretch(int t) => tokenPositions[t] >= HomeStretchStartPosition && tokenPositions[t] < HomePosition;
        public bool IsHome(int t) => tokenPositions[t] == HomePosition;

        public bool IsOnSafeTile(int t)
        {
            if (IsOnHomeStretch(t)) return true;
            if (!IsOnMainTrack(t)) return false;
            return Contains(SafeAbsoluteTiles,(byte)GetAbsolutePosition(t));
        }
        
        public bool Contains(byte[] array, byte value)
        {
            return Array.Exists(array, element => element == value);
        }

        public bool HasWon(int playerIndex)
        {
            int start = playerIndex * TokensPerPlayer;
            for (int i = 0; i < TokensPerPlayer; i++)
                if (!IsHome(start + i)) return false;
            return true;
        }

        /// PlantUML: MoveToken
        /// ```plantuml
        /// @startuml
        /// start
        /// :ValidateTokenIndex;
        /// if (steps <= 0 or IsHome?) then (yes)
        ///   :Return (noop);
        ///   stop
        /// endif
        /// :TryGetNewPosition(tokenIndex, steps, out pos);
        /// if (success?) then (no)
        ///   :Return (illegal -> noop);
        ///   stop
        /// else (yes)
        ///   :TokenPositions[tokenIndex] = pos;
        ///   if (IsOnMainTrack AND !IsOnSafeTile?) then (yes)
        ///     :CaptureTokensAt(tokenIndex);
        ///   endif
        ///   stop
        /// endif
        /// @enduml
        /// ```
        public void MoveToken(int tokenIndex, int steps)
        {
            ValidateTokenIndex(tokenIndex);
            if (steps <= 0 || IsHome(tokenIndex)) return;

            if (!TryGetNewPosition(tokenIndex, steps, out byte newPos))
                return;

            tokenPositions[tokenIndex] = newPos;
            if (IsOnMainTrack(tokenIndex) && !IsOnSafeTile(tokenIndex))
                CaptureTokensAt(tokenIndex);
        }

        /// PlantUML: TryGetNewPosition
        /// ```plantuml
        /// @startuml
        /// start
        /// if (IsHome?) then (yes) :fail; stop
        /// if (IsAtBase?) then (yes)
        ///   if (steps==6 AND start not blockaded?) then (yes)
        ///     :new=1; success; stop
        ///   else :fail; stop
        ///   endif
        /// endif
        /// if (IsOnMainTrack?) then (yes)
        ///   :check path for opponent blockade;
        ///   if (blocked?) then (yes) :fail; stop
        ///   if (current+steps <= 52?) then (yes) :new=current+steps; success; stop
        ///   else :stepsIntoHome = current+steps-52;
        ///        :target=53+stepsIntoHome-1 (<=59?);
        ///        if (<=59?) then (yes) :new=target; success; else :fail;
        ///   endif
        /// endif
        /// if (IsOnHomeStretch?) then (yes)
        ///   if (current+steps <= 59?) then (yes) :new=current+steps; success; else :fail;
        /// endif
        /// :fail;
        /// stop
        /// @enduml
        /// ```
        private bool TryGetNewPosition(int tokenIndex, int steps, out byte newPos)
        {
            newPos = tokenPositions[tokenIndex];
            if (IsHome(tokenIndex)) return false;

            if (IsAtBase(tokenIndex))
                return TryExitBase(tokenIndex, steps, out newPos);

            if (IsOnMainTrack(tokenIndex))
                return ComputeDestinationFromMainTrack(tokenIndex, steps, out newPos);

            if (IsOnHomeStretch(tokenIndex))
                return ComputeDestinationFromHomeStretch(tokenIndex, steps, out newPos);

            return false; // unknown state
        }

        /// PlantUML: GetOutOfBase
        /// ```plantuml
        /// @startuml
        /// start
        /// :ValidateTokenIndex;
        /// if (!IsAtBase) then (yes) :return; stop
        /// :absStart=StartAbsoluteTile(player);
        /// if (IsTileBlocked(absStart, player)?) then (yes) :return; else :Token=1;
        /// stop
        /// @enduml
        /// ```
        public void GetOutOfBase(int tokenIndex)
        {
            ValidateTokenIndex(tokenIndex);
            if (!IsAtBase(tokenIndex)) return;
            int p = PlayerOf(tokenIndex);
            int absStart = StartAbsoluteTile(p);
            if (IsTileBlocked(absStart, p)) return;
            tokenPositions[tokenIndex] = StartPosition; // safe tile; no capture
        }

        /// PlantUML: GetMovableTokens
        /// ```plantuml
        /// @startuml
        /// start
        /// if (dice in 1..6?) then (yes)
        ///   :add tokens where TryGetNewPosition succeeds;
        /// else :return empty;
        /// endif
        /// stop
        /// @enduml
        /// ```
        public List<int> GetMovableTokens(int playerIndex, int diceRoll)
        {
            var list = new List<int>();
            if (!IsDice(diceRoll)) return list;

            int start = playerIndex * TokensPerPlayer;
            for (int i = 0; i < TokensPerPlayer; i++)
            {
                int t = start + i;
                if (TryGetNewPosition(t, diceRoll, out _)) list.Add(t);
            }
            return list;
        }

        // ===== Internals: smaller helpers =====

        private bool TryExitBase(int tokenIndex, int steps, out byte newPos)
        {
            newPos = tokenPositions[tokenIndex];
            if (steps != ExitFromBaseAtRoll) return false;
            int p = PlayerOf(tokenIndex);
            int absStart = StartAbsoluteTile(p);
            if (IsTileBlocked(absStart, p)) return false;
            newPos = StartPosition;
            return true;
        }

        private bool ComputeDestinationFromMainTrack(int tokenIndex, int steps, out byte newPos)
        {
            newPos = tokenPositions[tokenIndex];
            // Check path blockades on traversed absolute tiles only while on main track
            if (PathBlockedByOpponent(tokenIndex, steps)) return false;

            int current = tokenPositions[tokenIndex];
            int target = current + steps;

            if (target <= TotalMainTrackTiles)
            {
                newPos = (byte)target;
                return true;
            }

            // Entering home stretch or beyond
            int stepsIntoHome = target - TotalMainTrackTiles; // >= 1
            int homeTarget = HomeStretchStartPosition + stepsIntoHome - 1; // 53..59
            if (homeTarget > HomePosition) return false; // overshoot
            newPos = (byte)homeTarget;
            return true;
        }

        private bool ComputeDestinationFromHomeStretch(int tokenIndex, int steps, out byte newPos)
        {
            newPos = tokenPositions[tokenIndex];
            int target = newPos + steps;
            if (target > HomePosition) return false; // overshoot
            newPos = (byte)target;
            return true;
        }

        private bool PathBlockedByOpponent(int tokenIndex, int steps)
        {
            foreach (var abs in EnumerateTraversedAbsoluteTiles(tokenIndex, steps))
                if (IsTileBlocked(abs, PlayerOf(tokenIndex))) return true;
            return false;
        }

        /// Enumerate absolute main‑track tiles this token would step on while still on 1..52 (not including start tile).
        private IEnumerable<int> EnumerateTraversedAbsoluteTiles(int tokenIndex, int steps)
        {
            if (!IsOnMainTrack(tokenIndex)) yield break;

            int currentRel = tokenPositions[tokenIndex]; // 1..52
            int p = PlayerOf(tokenIndex);
            int stepsOnMain = Math.Min(steps, TotalMainTrackTiles - currentRel);

            for (int i = 1; i <= stepsOnMain; i++)
            {
                byte nextRel = (byte)(currentRel + i);
                yield return ToAbsoluteMainTrack(nextRel, p);
            }
        }

        private void CaptureTokensAt(int movedTokenIndex)
        {
            if (!IsOnMainTrack(movedTokenIndex)) return;
            if (IsOnSafeTile(movedTokenIndex)) return;

            int p = PlayerOf(movedTokenIndex);
            int newAbs = GetAbsolutePosition(movedTokenIndex);

            for (int i = 0; i < tokenPositions.Length; i++)
            {
                if (PlayerOf(i) == p) continue;
                if (!IsOnMainTrack(i)) continue;
                if (GetAbsolutePosition(i) == newAbs)
                    tokenPositions[i] = BasePosition; // send to base
            }
        }

        private bool IsTileBlocked(int absolutePosition, int movingPlayer)
        {
            for (int opp = 0; opp < playerCount; opp++)
            {
                if (opp == movingPlayer) continue;

                int start = opp * TokensPerPlayer;
                int count = 0;
                for (int i = 0; i < TokensPerPlayer; i++)
                {
                    int t = start + i;
                    if (IsOnMainTrack(t) && GetAbsolutePosition(t) == absolutePosition)
                    {
                        count++;
                        if (count >= 2) return true; // opponent blockade
                    }
                }
            }
            return false;
        }

        // ===== Mapping utilities (kept public to simplify tests/docs) =====
        public int GetAbsolutePosition(int tokenIndex)
        {
            if (!IsOnMainTrack(tokenIndex)) return -1;
            int p = PlayerOf(tokenIndex);
            int rel = tokenPositions[tokenIndex];
            int off = GetPlayerTrackOffset(p);
            return (rel - 1 + off) % TotalMainTrackTiles + 1;
        }

        public int ToAbsoluteMainTrack(byte relativeMainTrackTile, int playerIndex)
        {
            int off = GetPlayerTrackOffset(playerIndex);
            return (relativeMainTrackTile - 1 + off) % TotalMainTrackTiles + 1;
        }

        public int StartAbsoluteTile(int playerIndex) => ToAbsoluteMainTrack(StartPosition, playerIndex);
        public int TokenIndex(int playerIndex, int tokenOrdinal) => playerIndex * TokensPerPlayer + tokenOrdinal;

        public byte RelativeForAbsolute(int playerIndex, int absoluteTile)
        {
            int off = GetPlayerTrackOffset(playerIndex);
            int rel = ((absoluteTile - 1 - off) % TotalMainTrackTiles + TotalMainTrackTiles) % TotalMainTrackTiles + 1;
            return (byte)rel;
        }

        // ===== Debug setters (optional) =====
        public void DebugSetTokenAtRelative(int playerIndex, int tokenOrdinal, int relative)
        {
            if (relative < 0 || relative > HomePosition) throw new ArgumentOutOfRangeException(nameof(relative));
            tokenPositions[TokenIndex(playerIndex, tokenOrdinal)] = (byte)relative;
        }
        public void DebugSetTokenAtAbsolute(int playerIndex, int tokenOrdinal, int absoluteTile)
        {
            if (absoluteTile < 1 || absoluteTile > TotalMainTrackTiles) throw new ArgumentOutOfRangeException(nameof(absoluteTile));
            tokenPositions[TokenIndex(playerIndex, tokenOrdinal)] = RelativeForAbsolute(playerIndex, absoluteTile);
        }
        public void DebugMakeBlockadeAtAbsolute(int ownerPlayerIndex, int absoluteTile)
        {
            DebugSetTokenAtAbsolute(ownerPlayerIndex, 0, absoluteTile);
            DebugSetTokenAtAbsolute(ownerPlayerIndex, 1, absoluteTile);
        }

        // ===== Small helpers =====
        private int PlayerOf(int tokenIndex) => tokenIndex / TokensPerPlayer;
        private bool IsDice(int n) => n >= 1 && n <= 6;

        private int GetPlayerTrackOffset(int playerIndex)
        {
            if (playerCount == 2) return playerIndex * 2 * PlayerTrackOffset; // 0, 26
            return playerIndex * PlayerTrackOffset;                            // 0, 13, 26, 39
        }

        private void ValidateTokenIndex(int tokenIndex)
        {
            if (tokenIndex < 0 || tokenIndex >= tokenPositions.Length)
                throw new ArgumentOutOfRangeException(nameof(tokenIndex));
        }
    }
}
