--Creating the users table
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100),
    email VARCHAR(100) UNIQUE NOT NULL,
    password_hash TEXT NOT NULL,
    role VARCHAR(10) CHECK (role IN ('host', 'guest')),
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

--Creating the listings table
CREATE TABLE listings (
    id SERIAL PRIMARY KEY,
    user_id INTEGER REFERENCES users(id) ON DELETE CASCADE,
    title VARCHAR(200) NOT NULL,
    description TEXT,
    location VARCHAR(255) NOT NULL,
    price_per_night NUMERIC(10, 2) NOT NULL,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

--Creating the bookings table
CREATE TABLE bookings (
    id SERIAL PRIMARY KEY,
    user_id INTEGER REFERENCES users(id) ON DELETE CASCADE,
    listing_id INTEGER REFERENCES listings(id) ON DELETE CASCADE,
    start_date DATE NOT NULL,
    end_date DATE NOT NULL,
    total_price NUMERIC(10, 2),
    status VARCHAR(20) CHECK (status IN ('pending', 'confirmed', 'cancelled')) DEFAULT 'pending'
    CHECK (end_date > start_date)
);

--Creating the reviews table
CREATE TABLE reviews (
    id SERIAL PRIMARY KEY,
    user_id INTEGER REFERENCES users(id) ON DELETE CASCADE,
    listing_id INTEGER REFERENCES listings(id) ON DELETE CASCADE,
    rating INTEGER CHECK (rating BETWEEN 1 AND 5),
    comment TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

--Creating the photos table
CREATE TABLE photos (
    id SERIAL PRIMARY KEY,
    listing_id INTEGER REFERENCES listings(id) ON DELETE CASCADE,
    image_url TEXT NOT NULL,
    is_cover BOOLEAN DEFAULT FALSE
);

--Creating the payments table
CREATE TABLE payments (
    id SERIAL PRIMARY KEY,
    booking_id INTEGER REFERENCES bookings(id) ON DELETE CASCADE,
    user_id INTEGER REFERENCES users(id) ON DELETE SET NULL,
    amount NUMERIC(10, 2) NOT NULL,
    payment_method VARCHAR(50),
    status VARCHAR(20) CHECK (status IN ('pending', 'completed', 'failed', 'refunded')) DEFAULT 'pending',
    transaction_id VARCHAR(255),
    paid_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
    CHECK (amount > 0)
);

--Procedure for creating a new reservation
CREATE OR REPLACE PROCEDURE sp_create_booking(
    IN p_user_id INTEGER,
    IN p_listing_id INTEGER,
    IN p_start_date DATE,
    IN p_end_date DATE,
    IN p_total_price NUMERIC
)
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO bookings(user_id, listing_id, start_date, end_date, total_price, status)
    VALUES (p_user_id, p_listing_id, p_start_date, p_end_date, p_total_price, 'pending');
END;
$$;

--Procedure for updating the listing status
CREATE OR REPLACE PROCEDURE sp_update_listing_status(
    IN p_listing_id INTEGER,
    IN p_status BOOLEAN
)
LANGUAGE plpgsql
AS $$
BEGIN
    UPDATE listings
    SET is_active = p_status
    WHERE id = p_listing_id;
END;
$$;

--Function to calculate the total cost according to the dates
CREATE OR REPLACE FUNCTION fn_calculate_total_price(
    p_listing_id INTEGER,
    p_start_date DATE,
    p_end_date DATE
)
RETURNS NUMERIC
LANGUAGE plpgsql
AS $$
DECLARE
    nights INTEGER;
    price NUMERIC;
BEGIN
    SELECT price_per_night INTO price FROM listings WHERE id = p_listing_id;
    nights := (p_end_date - p_start_date);
    RETURN nights * price;
END;
$$;

--Function to get the listing count for a host
CREATE OR REPLACE FUNCTION fn_get_host_listing_count(p_host_id INTEGER)
RETURNS INTEGER
LANGUAGE sql
AS $$
    SELECT COUNT(*) FROM listings WHERE user_id = p_host_id;
$$;

--Function to create a payment automatically after a reservation created
CREATE OR REPLACE FUNCTION fn_auto_create_payment()
RETURNS TRIGGER AS $$
BEGIN
    INSERT INTO payments(booking_id, user_id, amount, status)
    VALUES (NEW.id, NEW.user_id, NEW.total_price, 'pending');
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

--Function to validate the booking dates
CREATE OR REPLACE FUNCTION fn_validate_booking_dates()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.end_date <= NEW.start_date THEN
        RAISE EXCEPTION 'End date must be after start date';
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

--Trigger to create a new payment automatically after a reservation created
CREATE TRIGGER tg_auto_create_payment
AFTER INSERT ON bookings
FOR EACH ROW
EXECUTE FUNCTION fn_auto_create_payment();

--Trigger to check booking dates
CREATE TRIGGER tg_check_booking_dates
BEFORE INSERT OR UPDATE ON bookings
FOR EACH ROW
EXECUTE FUNCTION fn_validate_booking_dates();
