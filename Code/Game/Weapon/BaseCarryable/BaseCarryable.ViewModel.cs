public partial class BaseCarryable : Component
{
	[Property, Feature( "ViewModel" )] public GameObject ViewModelPrefab { get; set; }

	private bool _suppressViewModel;

	public void Deploy()
	{
		_suppressViewModel = false;

		if ( !ViewModel.IsValid() )
			CreateViewModel();

		if ( ViewModel.IsValid() && ViewModel.GetComponent<ViewModel>() is { IsValid: true } viewModel )
		{
			viewModel.Deploy();
			return;
		}

		WeaponModel?.Deploy();
	}

	public void Holster()
	{
		if ( ViewModel.IsValid() && ViewModel.GetComponent<ViewModel>() is { IsValid: true } viewModel )
		{
			viewModel.Holster();
			return;
		}

		WeaponModel?.Holster();
	}

	public void HolsterForPickup()
	{
		Holster();
		_suppressViewModel = true;
	}

	protected void CreateViewModel()
	{
		if ( _suppressViewModel )
			return;

		if ( ViewModel.IsValid() )
			return;

		DestroyViewModel();

		if ( ViewModelPrefab is null )
			return;

		var player = Owner;
		if ( player is null || player.Controller is null || player.Controller.Renderer is null || !player.IsLocalPlayer ) return;

		ViewModel = ViewModelPrefab.Clone( new CloneConfig { Parent = GameObject, StartEnabled = false, Transform = global::Transform.Zero } );
		ViewModel.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked | GameObjectFlags.Absolute;
		ViewModel.Enabled = true;
		ViewModel.Tags.Add( "firstperson", "viewmodel" );

		var vm = ViewModel.GetComponent<ViewModel>();

		if ( !vm.IsValid() )
			return;

		vm.Deploy();
	}

	protected void DestroyViewModel()
	{
		if ( !ViewModel.IsValid() )
			return;

		ViewModel?.Destroy();
		ViewModel = default;
	}
}
