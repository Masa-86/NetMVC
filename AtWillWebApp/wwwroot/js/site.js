// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

function addTableRow(tableId, inputId) {
    const inputValue = document.getElementById(inputId);

    let table = document.getElementById(tableId);
    let row = table.insertRow(-1);
    let cell = row.insertCell(-1);

    cell.innerHTML = inputValue.value;
}
