using System;
using UnityEngine;

public sealed class UpgradeSelection
{
    private readonly RunDirector _run;
    public int Offer { get; private set; } = -1;
    public int Slot { get; private set; } = -1;
    public bool AwaitingConfirmation { get; private set; }
    public event Action Changed;

    public UpgradeSelection(RunDirector run)
    {
        _run = run;
    }

    public void Reset()
    {
        Offer = -1;
        Slot = -1;
        AwaitingConfirmation = false;
        Changed?.Invoke();
    }

    public void SelectOffer(int index)
    {
        if (!_run.AwaitingReward || AwaitingConfirmation || index < 0 || index >= _run.Offers.Length)
        {
            return;
        }
        Offer = Offer == index ? -1 : index;
        AwaitingConfirmation = Offer >= 0 && Slot >= 0;
        Changed?.Invoke();
    }

    public void SelectSlot(int index)
    {
        if (!_run.AwaitingReward || AwaitingConfirmation || index < 0 || index >= _run.Drum.MaxBullets)
        {
            return;
        }
        if (Offer >= 0)
        {
            Slot = index;
            AwaitingConfirmation = true;
        }
        else if (Slot < 0)
        {
            Slot = index;
        }
        else if (Slot == index)
        {
            Slot = -1;
        }
        else
        {
            _run.SwapRewardCartridges(Slot, index);
            Slot = -1;
        }
        Changed?.Invoke();
    }

    public void Confirm()
    {
        if (!AwaitingConfirmation || !_run.AwaitingReward)
        {
            return;
        }
        int offer = Offer;
        int slot = Slot;
        Reset();
        _run.Choose(offer, slot);
    }

    public void Cancel()
    {
        Reset();
    }
}
