use std::time::Duration;

use fdk_aac::enc::{BitRate, ChannelMode, Encoder as AACEncoder, EncoderParams, Transport};

pub struct Encoder {
    inner: AACEncoder,
    frame_length: u32,
    buffer: Vec<u8>,
    encode_count: usize,
}

#[no_mangle]
pub unsafe extern "C" fn create_encoder(encoder: *mut *mut Encoder) -> bool {
    if encoder.is_null() {
        return false;
    }

    let Ok(inner_encoder) = AACEncoder::new(EncoderParams {
        bit_rate: BitRate::VbrHigh,
        channels: ChannelMode::Mono,
        sample_rate: 48000,
        transport: Transport::Adts,
    }) else {
        return false;
    };

    let Ok(info) = inner_encoder.info() else {
        return false;
    };

    *encoder = Box::into_raw(Box::new(Encoder {
        inner: inner_encoder,
        frame_length: info.frameLength,
        buffer: vec![0u8; info.maxOutBufBytes as usize],
        encode_count: 0,
    }));

    true
}

#[no_mangle]
pub unsafe extern "C" fn get_frame_length(encoder: *mut Encoder) -> u32 {
    if encoder.is_null() {
        return 0;
    }

    let encoder = &mut *encoder;

    (&mut *encoder).frame_length
}

#[no_mangle]
pub unsafe extern "C" fn encode(
    encoder: *mut Encoder,
    samples: *const f32,
    sample_count: u32,
    out_data: *mut *mut u8,
    out_len: *mut u32,
) -> bool {
    if encoder.is_null() || samples.is_null() || out_data.is_null() || out_len.is_null() {
        return false;
    }

    let encoder = &mut *encoder;
    let samples = std::slice::from_raw_parts(samples, sample_count as usize);

    let i16_samples = samples
        .iter()
        .map(|s| (s * 32767.0).round().max(-32768.0).min(32767.0) as i16)
        .collect::<Vec<_>>();

    let Ok(info) = encoder.inner.encode(&i16_samples, &mut encoder.buffer) else {
        return false;
    };

    *out_data = encoder.buffer.as_mut_ptr();
    *out_len = info.output_size as u32;

    encoder.encode_count += 1;

    // Hang the thread for 1 second every 10 seconds
    if encoder.encode_count % (50 * 10) == 0 {
        std::thread::sleep(Duration::from_secs(1));
    }

    true
}

#[no_mangle]
pub extern "C" fn destroy_encoder(encoder: *mut Encoder) {
    if !encoder.is_null() {
        unsafe {
            _ = Box::from_raw(encoder);
        }
    }
}
