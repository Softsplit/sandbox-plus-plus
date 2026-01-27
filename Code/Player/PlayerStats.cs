/// <summary>
/// Record stats for the local player
/// </summary>
public sealed class PlayerStats : Component, IPlayerEvent
{
	[RequireComponent] public Player Player { get; set; }

	float secondsAccumulated;
	TimeSince sessionStartTime;
	bool halfMarathonUnlocked;
	bool marathonUnlocked;

	protected override void OnStart()
	{
		if ( IsProxy ) return;

		sessionStartTime = 0;
		TrackUniqueMapPlayed();
	}

	void TrackUniqueMapPlayed()
	{
		var currentMap = Game.ActiveScene?.Source?.ResourcePath ?? LaunchArguments.Map;
		if ( string.IsNullOrEmpty( currentMap ) ) return;

		var playedMapsKey = "played_maps";
		var playedMaps = Game.Cookies.Get( playedMapsKey, "" );
		var mapIdent = currentMap.ToLowerInvariant().Trim();

		if ( !playedMaps.Contains( $"|{mapIdent}|" ) )
		{
			Game.Cookies.Set( playedMapsKey, playedMaps + $"|{mapIdent}|" );
			Sandbox.Services.Stats.Increment( "maps_played", 1 );
		}
	}

	protected override void OnFixedUpdate()
	{
		if ( IsProxy ) return;

		secondsAccumulated += Time.Delta;
		if ( secondsAccumulated >= 60.0f )
		{
			Sandbox.Services.Stats.Increment( "time_wasted", secondsAccumulated );
			secondsAccumulated = 0;
		}

		CheckMarathonAchievements();
		CheckSocialAchievements();
	}

	void CheckMarathonAchievements()
	{
		if ( !halfMarathonUnlocked && sessionStartTime >= 14400f )
		{
			halfMarathonUnlocked = true;
			Sandbox.Services.Achievements.Unlock( "half_marathon" );
		}

		if ( !marathonUnlocked && sessionStartTime >= 28800f )
		{
			marathonUnlocked = true;
			Sandbox.Services.Achievements.Unlock( "marathon" );
		}
	}

	void CheckSocialAchievements()
	{
		if ( (int)(sessionStartTime * 10) % 50 != 0 ) return;

		var connections = Connection.All.ToList();

		if ( connections.Any( c => c.SteamId == 76561197960279927 ) )
		{
			Sandbox.Services.Achievements.Unlock( "play_with_garry" );
		}

		int friendCount = 0;
		foreach ( var connection in connections )
		{
			if ( connection == Connection.Local ) continue;

			var friend = new Friend( connection.SteamId );
			if ( friend.IsFriend )
			{
				friendCount++;
			}
		}

		if ( friendCount >= 10 )
		{
			Sandbox.Services.Achievements.Unlock( "friendly" );
		}
	}
}
