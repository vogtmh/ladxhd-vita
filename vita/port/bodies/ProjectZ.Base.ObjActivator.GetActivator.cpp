{
    // Expression trees are replaced by the constructor invoker from the generated reflection table.
    return ::System::Fn<T(::System::Array1<::System::Object*>*)>::Make([ctor](::System::Array1<::System::Object*>* args) -> T {
        return ::System::cast<std::remove_pointer_t<T>>(ctor->Invoke(args));
    });
}
