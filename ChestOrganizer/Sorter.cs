using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace ChestOrganizer;
public static class Sorter {
    public static void Sort(this IInventory inventory, IComparer<ItemStack> comparer, ICoreClientAPI api, bool merge = true) {
        int n = inventory.Count;
        var current = Enumerable
            .Range(0, n)
            .ToArray();
        var from = current
            .ToArray();
        var order = current
            .Order(new Comparer(inventory, comparer))
            .ToArray();
        var player = api.World.Player;
        var manager = player.InventoryManager;

        // Move items to desired order.
        for (int i = 0; i < n; i++) {
            int j = order[i];
            int k = current[j];
            if (k == i) continue;

            var targetSlot = inventory[i];
            var sourceSlot = inventory[k];

            if (targetSlot.Empty) {
                if (!sourceSlot.Empty) {
                    Move(sourceSlot, targetSlot);
                }
            } else if (sourceSlot.Empty) {
                Move(targetSlot, sourceSlot);
            } else {
                Flip(sourceSlot, targetSlot);
            }

            (from[i], from[k]) = (from[k], from[i]);
            current[from[i]] = i;
            current[from[k]] = k;
        }

        if (!merge) return;

        // Merge stacks.
        bool changed = false;
        for (int i = 0, j = 1; i < n - 1 && j < n; ) {
            var target = inventory[i];
            var source = inventory[j];
            if (changed && target.CanTakeFrom(source)) {
                changed = Move(source, target);
                if (source.Empty) j++;
            } else { 
                changed = true;
                i++;
                if (i >= j) j = i + 1;
            }
        }

        bool Move(ItemSlot from, ItemSlot to) {
            int pre = from.StackSize;
            int n = from.GetRemainingSlotSpace(to.Itemstack);
            ItemStackMoveOperation op = new(api.World, EnumMouseButton.Left, 0, EnumMergePriority.AutoMerge, n);
            op.ActingPlayer = player;
            SendPacket(manager.TryTransferTo(from, to, ref op));
            return pre != from.StackSize;
        }

        void Flip(ItemSlot a, ItemSlot b) {
            // Some composite inventories (e.g. InventoryPlayerBackPack in VS 1.22+) have
            // virtual/wrapper slots whose DidModifyItemSlot validation fails across
            // sub-inventories. Catch the resulting ArgumentException to avoid crashing.
            try {
                if (a.TryFlipWith(b)) {
                    SendPacket(a.Inventory.InvNetworkUtil.GetFlipSlotsPacket(b.Inventory, SlotId(b), SlotId(a)));
                }
            } catch (ArgumentException) {
                // Swap failed: fall back to a two-move approach via Move to preserve
                // as much ordering as possible.
                if (!a.Empty && !b.Empty) {
                    // If Move partially moves (e.g. stackable), accept it; otherwise skip.
                    Move(a, b);
                }
            }
        }

        static int SlotId(ItemSlot slot) 
            => slot.Inventory.GetSlotId(slot);

        void SendPacket(object obj) {
            if (obj is Packet_Client packet) {
                api.Network.SendPacketClient(packet);
            }
        }
    }

    private class Comparer : IComparer<int> {
        private readonly IInventory inventory;
        private readonly IComparer<ItemStack> comparer;

        public Comparer(IInventory inventory, IComparer<ItemStack> comparer) {
            this.inventory = inventory;
            this.comparer = comparer;
        }

        public int Compare(int x, int y) 
            => comparer.Compare(inventory[x].Itemstack, inventory[y].Itemstack);
    }
}
