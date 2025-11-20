namespace SandboxClassic.UI;

public partial class Chat
{
	[Rpc.Broadcast]
	public static void AddChatEntry( string name, string message, long playerId = 0, bool isInfo = false )
	{
		Current?.AddEntry( name, message, playerId, isInfo );

		// Only log clientside if we're not the listen server host
		if ( !Networking.IsHost )
		{
			Log.Info( $"{name}: {message}" );
		}
	}

	[ConCmd( "sandbox_say", ConVarFlags.Server )]
	public static void Say( Connection caller, string message )
	{
		if ( caller == null ) return;

		// todo - reject more stuff
		if ( message.Contains( '\n' ) || message.Contains( '\r' ) )
			return;

		Log.Info( $"{caller}: {message}" );
		AddChatEntry( caller.DisplayName, message, caller.SteamId );
	}
}
