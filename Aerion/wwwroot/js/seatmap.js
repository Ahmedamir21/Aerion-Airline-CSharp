// Seat picking on the booking page: tracks the chosen seats, the total and the hidden form field.
(function () {
    var MAX_SEATS = 9;
    var plane = document.getElementById('plane');
    if (!plane) return;

    var list = document.getElementById('sel-list');
    var totalEl = document.getElementById('sel-total');
    var seatsField = document.getElementById('seats-field');
    var confirmButton = document.getElementById('confirm');
    var milesSelect = document.getElementById('miles-units');
    var perUnit = window.aerionDiscountPerUnit || 0;
    var picked = {};

    function money(value) {
        return '$' + value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    function discountedTotal(sum) {
        var units = milesSelect ? parseInt(milesSelect.value || '0', 10) : 0;
        return units ? sum * (1 - units * perUnit / 100) : sum;
    }

    function render() {
        var seats = Object.keys(picked);
        var sum = 0;
        list.innerHTML = seats.length ? '' : '<li class="muted">No seats selected yet</li>';
        seats.forEach(function (seat) {
            sum += picked[seat].price;
            var item = document.createElement('li');
            item.textContent = seat + ' · ' + picked[seat].cabin + ' · ' + money(picked[seat].price);
            list.appendChild(item);
        });
        totalEl.textContent = money(discountedTotal(sum));
        seatsField.value = seats.join(',');
        confirmButton.disabled = seats.length === 0;
    }

    function toggle(seatButton) {
        var seat = seatButton.dataset.seat;
        if (picked[seat]) {
            delete picked[seat];
            seatButton.classList.remove('sel');
        } else if (Object.keys(picked).length < MAX_SEATS) {
            picked[seat] = { price: parseFloat(seatButton.dataset.price), cabin: seatButton.dataset.class };
            seatButton.classList.add('sel');
        }
        render();
    }

    plane.addEventListener('click', function (e) {
        var seatButton = e.target.closest('.seat.free');
        if (seatButton) toggle(seatButton);
    });

    if (milesSelect) milesSelect.addEventListener('change', render);

    document.querySelectorAll('.fare-card').forEach(function (card) {
        card.addEventListener('click', function () {
            var cabin = plane.querySelector('[data-cabin="' + card.dataset.focus + '"]');
            if (cabin) cabin.scrollIntoView({ behavior: 'smooth', block: 'center' });
        });
    });
})();
