afterEach(() => {
    if (typeof document !== "undefined") {
        document.body.innerHTML = "";
    }

    jest.clearAllMocks();
});
