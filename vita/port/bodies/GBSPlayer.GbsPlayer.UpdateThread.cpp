{
    if (this->_exitThread) return;
    ::System::Threading::LockGuard lk(this->_updateLock);
    int32_t req = this->_pendingTrack;
    if (req >= 0) {
        this->_pendingTrack = -1;
        uint8_t song = (uint8_t)req;
        if (this->CurrentTrack != song) this->StartTrack_NoLock(song);
    }
    // Called once per game update instead of every 5 ms, so run the emulator until its buffer queue is full
    // (each Update() is capped at 3 chunks); otherwise music underruns whenever the frame rate drops.
    for (int i = 0; i < 8 && this->Cpu->IsRunning; i++) this->Cpu->Update();
}
