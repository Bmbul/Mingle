using Mingle.Engine;

namespace Mingle.Client.Services;

/// <summary>The line above a card that says who answers it, shared by pass-and-play and online play.</summary>
public static class WhoLine
{
    public static string For(Text t, GameView view, TurnStyle turnStyle)
    {
        var turn = view.TurnPlayerName ?? "";
        var kind = CardKinds.Get(view.KindId!);
        if (kind.Audience == Audience.AboutTurnPlayer) return t["Play_Subject", turn];
        if (view.Input == InputMode.Spoken)
            return turnStyle == TurnStyle.TakeTurns ? t["Play_OutLoudOne", turn] : t["Play_OutLoudAll"];
        return view.AnswererIds.Length == 1 ? t["Play_Turn", turn] : t["Play_EveryoneAnswers"];
    }
}
