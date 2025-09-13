public partial class BaseWeapon
{
	private bool isReloading;

	public bool CanReload()
	{
		if ( isReloading ) return false;

		var owner = Owner;
		if ( !owner.IsValid() )
			return false;

		return true;
	}

	public bool IsReloading() => isReloading;

	public virtual async void OnReloadStart()
	{
		if ( !CanReload() )
			return;

		if ( isReloading )
			return;

		isReloading = true;
		await ReloadAsync();
	}

	[Rpc.Broadcast]
	private void BroadcastReload()
	{
		if ( !Owner.IsValid() ) return;

		Assert.True( Owner.Controller.IsValid(), "BaseWeapon::BroadcastReload - Player Controller is invalid!" );
		Assert.True( Owner.Controller.Renderer.IsValid(), "BaseWeapon::BroadcastReload - Renderer is invalid!" );

		Owner.Controller.Renderer.Set( "b_reload", true );
	}

	protected virtual async Task ReloadAsync()
	{
		try
		{
			ViewModel?.RunEvent<ViewModel>( x => x.OnReloadStart() );

			BroadcastReload();

			await Task.Delay( (int)(ReloadTime * 1000) );
		}
		finally
		{
			isReloading = false;

			ViewModel?.RunEvent<ViewModel>( x => x.OnReloadFinish() );
		}
	}
}
