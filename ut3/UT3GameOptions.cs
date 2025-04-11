// can't mark as readonly (Player.IO doesn't allow System.Runtime.CompilerServices.IsReadOnlyAttribute)
/*readonly*/ struct UT3GameOptions {
	public readonly bool Inverted;
	public readonly bool Quick;
	public readonly bool Checked;
	public readonly bool AnyBoard;

	public UT3GameOptions(bool inverted, bool quick, bool check, bool anyBoard) {
		Inverted = inverted;
		Quick = quick;
		Checked = check;
		AnyBoard = anyBoard;
	}
}
