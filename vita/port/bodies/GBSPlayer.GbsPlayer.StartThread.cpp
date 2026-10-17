{
    // No background thread: the platform frame hook calls UpdateThread() once per update.
    this->_exitThread = false;
}
