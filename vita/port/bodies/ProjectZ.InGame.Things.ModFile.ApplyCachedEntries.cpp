{
    ::System::Collections::Generic::List<::System::ValueTuple<::System::String*, ::System::String*>>* entries = nullptr;
    if (_advancedCache == nullptr || !_advancedCache->TryGetValue(className, entries)) return;
    for (auto kv : ::System::Each(entries)) {
        auto* field = inputClass->GetType()->GetField(kv.Item1, flags);
        if (!field) continue;
        try {
            field->SetValue(inputClass, ::System::Convert::ChangeType(kv.Item2, field->get_FieldType(), ::System::Globalization::CultureInfo::get_InvariantCulture()));
        } catch (::System::Exception*) {
        }
    }
}
