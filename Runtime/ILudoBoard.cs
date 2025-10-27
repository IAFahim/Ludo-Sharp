using System.Collections.Generic;

namespace Ludo
{
    public interface ILudoBoard
    {
        // Constants (as static for interface compatibility in C# 8+)
        static byte Base => LudoBoard.Base;
        static byte MainStart => LudoBoard.MainStart;
        static byte MainEnd => LudoBoard.MainEnd;
        static byte HomeStart => LudoBoard.HomeStart;
        static byte Home => LudoBoard.Home;
        static byte ExitRoll => LudoBoard.ExitRoll;
        static byte Tokens => LudoBoard.Tokens;
        static byte[] SafeAbsoluteTiles => LudoBoard.SafeAbsoluteTiles;
        static byte StepsToHome => LudoBoard.StepsToHome;

        // State properties
        byte[] TokenPositions { get; }
        int PlayerCount { get; }

        // Queries
        bool IsAtBase(int t);
        bool IsOnMainTrack(int t);
        bool IsOnHomeStretch(int t);
        bool IsHome(int t);
        bool IsOnSafeTile(int t);
        bool HasWon(int playerIndex);

        // Actions
        void MoveToken(int tokenIndex, int steps);
        void GetOutOfBase(int tokenIndex);
        List<int> GetMovableTokens(int playerIndex, int diceRoll);

        // Mapping utilities
        int GetAbsolutePosition(int tokenIndex);
        int ToAbsoluteMainTrack(byte relativeMainTrackTile, int playerIndex);
        int StartAbsoluteTile(int playerIndex);
        int TokenIndex(int playerIndex, int tokenOrdinal);
        byte RelativeForAbsolute(int playerIndex, int absoluteTile);

        // Debug setters (optional, for testing)
        void DebugSetTokenAtRelative(int playerIndex, int tokenOrdinal, int relative);
        void DebugSetTokenAtAbsolute(int playerIndex, int tokenOrdinal, int absoluteTile);
        void DebugMakeBlockadeAtAbsolute(int ownerPlayerIndex, int absoluteTile);
    }
}