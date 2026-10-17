{
    auto flags = ::System::Reflection::BindingFlags::Instance | ::System::Reflection::BindingFlags::NonPublic | ::System::Reflection::BindingFlags::Public;
    if (_advancedCache != nullptr) ApplyCachedEntries(inputClass->GetType()->get_Name(), inputClass, flags);
    if (!::ProjectZ::InGame::Things::GameFS::Exists(modFile)) return;
    for (auto* line : ::System::Each(::ProjectZ::InGame::Things::GameFS::ReadAllLines(modFile))) {
        if (::System::String::IsNullOrWhiteSpace(line) || line->StartsWith(STR(u"//"))) continue;
        auto* split = line->Split(::System::Array1<char16_t>::Of({u'=', u'/'}));
        if (split->get_Length() < 2) continue;
        auto* field = inputClass->GetType()->GetField(split->at(0)->Trim(), flags);
        if (!field) continue;
        try {
            field->SetValue(inputClass, ::System::Convert::ChangeType(split->at(1)->Trim(), field->get_FieldType(), ::System::Globalization::CultureInfo::get_InvariantCulture()));
        } catch (::System::Exception*) {
        }
    }
}
