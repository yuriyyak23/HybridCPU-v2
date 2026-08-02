namespace YAKSys_Hybrid_CPU.Core;

/// <summary>
/// Reference-owned storage for the core-local StreamEngine/vector scratch
/// buffers, buffer selector and bank-conflict controllers. This container is
/// neither architectural state nor a timed-memory authority.
/// </summary>
internal sealed class ScratchState
{
    internal byte[] ScratchA = null!;
    internal byte[] ScratchB = null!;
    internal byte[] ScratchDst = null!;
    internal byte[] ScratchIndex = null!;

    internal byte[] ScratchA_DB0 = null!;
    internal byte[] ScratchB_DB0 = null!;
    internal byte[] ScratchA_DB1 = null!;
    internal byte[] ScratchB_DB1 = null!;
    internal byte[] ScratchDst_DB0 = null!;
    internal byte[] ScratchDst_DB1 = null!;

    internal ScratchBankController BankedScratchA;
    internal ScratchBankController BankedScratchB;
    internal ScratchBankController BankedScratchDst;

    internal int ActiveBufferSet;
}
