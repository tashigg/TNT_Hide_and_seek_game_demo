using System;
using System.Collections.Generic;
using Unity.Services.Lobbies.Models;

public class LobbyPlayerdata
{
    public string Id { get; private set; }

    public string Name { get; private set; }

    public string Role { get; private set; }

    public bool IsReady { get; private set; }

    public void Initialize(string id, string name, string role, bool isReady)
    {
        Id = id;
        Name = name;
        Role = role;
        IsReady = isReady;
    }

    public void Initialize(Dictionary<string, PlayerDataObject> playerData)
    {
        UpdateState(playerData);
    }

    public void UpdateState(Dictionary<string, PlayerDataObject> playerData)
    {
        if (playerData.ContainsKey("Id"))
        {
            Id = playerData["Id"].Value;
        }
        if (playerData.ContainsKey("Name"))
        {
            Name = playerData["Name"].Value;
        }
        if (playerData.ContainsKey("Role"))
        {
            Role = playerData["Role"].Value;
        }
        if (playerData.ContainsKey("IsReady"))
        {
            IsReady = playerData["IsReady"].Value == "True";
        }
        
    }

    public Dictionary<string, string> Serialize()
    {
        return new Dictionary<string, string>()
        {
            { "Id", Id },
            { "Name", Name },
            { "Role", Role },
            { "IsReady", IsReady.ToString() }
        };
    }
}

