using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class CameraTarget : EntityLinkMono
{
    public static CameraTarget MainTarget;

    public int PlayerIndex
    {
        get
        {
            if (!HasLink()) return -1;
            using var playersE = Game.m_PlayerQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < playersE.Length; i++)
            {
                if (playersE[i] == Entity)
                {
                    using var players = Game.m_PlayerQuery.ToComponentDataArray<PlayerControlled>(Allocator.Temp);
                    return players[i].Index;
                }
            }
            return -1;
        }
    }

    private void Start()
    {
        if (PlayerIndex == Game.PlayerIndex)
        {
            MainTarget = this;
        }
    }
}