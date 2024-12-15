// File: wwwroot/js/dragdrop.js

window.dragDropInterop = {
    setData: function (event, key, value) {
        event.dataTransfer.setData(key, value);
    },
    getData: function (event, key) {
        return event.dataTransfer.getData(key);
    }
};
